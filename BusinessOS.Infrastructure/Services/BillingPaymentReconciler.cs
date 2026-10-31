using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class BillingPaymentReconciler : IBillingPaymentReconciler
{
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly ILogger<BillingPaymentReconciler> _logger;

    public BillingPaymentReconciler(
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        ILogger<BillingPaymentReconciler> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<Guid?> ApplyVerifiedResultAsync(
        PaymentProviderType provider,
        string operationKey,
        BillingTransactionStatus status,
        string? providerReference,
        decimal? amount,
        string? currency,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await db.BillingTransactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => !x.IsDeleted &&
                    x.Provider == provider &&
                    (x.TransactionId == operationKey ||
                     x.ProviderOperationKey == operationKey ||
                     x.ProviderReference == operationKey),
                cancellationToken);

        if (transaction is null)
            return null;

        if (!string.IsNullOrWhiteSpace(currency) &&
            !string.Equals(transaction.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            transaction.Status = BillingTransactionStatus.RequiresReconciliation;
            transaction.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "Billing transaction {TransactionId} currency did not match the verified provider result",
                transaction.TransactionId);
            return transaction.TenantId;
        }

        if (amount.HasValue && Math.Abs(transaction.Amount - amount.Value) > 0.01m &&
            status == BillingTransactionStatus.Completed)
        {
            transaction.Status = BillingTransactionStatus.RequiresReconciliation;
            transaction.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "Billing transaction {TransactionId} amount {StoredAmount} did not match provider amount {ProviderAmount}",
                transaction.TransactionId,
                transaction.Amount,
                amount);
            return transaction.TenantId;
        }

        if (!BillingTransactionTransitions.CanApplyVerifiedProviderResult(transaction.Status, status))
        {
            _logger.LogInformation(
                "Ignored provider status {Status} for billing transaction {TransactionId} already in {CurrentStatus}",
                status,
                transaction.TransactionId,
                transaction.Status);
            return transaction.TenantId;
        }

        transaction.Status = status;
        transaction.ProviderReference = string.IsNullOrWhiteSpace(providerReference)
            ? transaction.ProviderReference
            : providerReference;
        transaction.UpdatedAt = DateTime.UtcNow;
        if (status == BillingTransactionStatus.Completed)
            transaction.CompletedAt ??= DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Billing transaction {TransactionId} moved to {Status} for tenant {TenantId}",
            transaction.TransactionId,
            status,
            transaction.TenantId);
        return transaction.TenantId;
    }
}

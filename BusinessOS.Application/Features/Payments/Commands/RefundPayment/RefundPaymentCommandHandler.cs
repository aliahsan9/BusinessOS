using System.Text.Json;
using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Payments.Commands.RefundPayment;

public sealed class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;

    public RefundPaymentCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IIdempotencyService idempotency,
        ILogger<RefundPaymentCommandHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task<Guid> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        if (!IdempotencyKeyRules.IsValid(request.IdempotencyKey))
            throw new BadRequestException("Idempotency-Key must be 1-128 visible ASCII characters without spaces.");

        if (request.Amount <= 0)
            throw new BadRequestException("Refund amount must be greater than zero.");

        var amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var payment = await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PaymentId, cancellationToken);

        if (payment is null)
            throw new NotFoundException("Payment not found.");

        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? payment.Currency
            : request.Currency.Trim().ToUpperInvariant();

        var tenantId = _tenantProvider.TenantId;
        var hash = CanonicalRequestHasher.PaymentRefund(tenantId, payment.Id, amount, currency);
        var outcome = await _idempotency.BeginLocalAsync(
            IdempotencyOperations.PaymentRefund,
            request.IdempotencyKey,
            hash,
            cancellationToken);

        switch (outcome.Kind)
        {
            case IdempotencyOutcomeKind.Replay:
                return outcome.ResourceId ?? payment.Id;
            case IdempotencyOutcomeKind.Conflict:
                throw new ConflictException(
                    "This Idempotency-Key was already used for a different refund request.",
                    ErrorCodes.IdempotencyKeyReused);
            case IdempotencyOutcomeKind.InProgress:
                throw new ConflictException(
                    "This refund is still processing. Retry the same request.",
                    ErrorCodes.IdempotencyInProgress);
        }

        var staged = outcome.Record
            ?? throw new ConflictException(
                "This refund is still processing. Retry the same request.",
                ErrorCodes.IdempotencyInProgress);

        if (!string.IsNullOrWhiteSpace(request.Currency) &&
            !string.Equals(currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _idempotency.Detach(staged);
            throw new BadRequestException("Refund currency does not match the payment currency.");
        }

        if (!string.Equals(payment.Status, PaymentStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
        {
            _idempotency.Detach(staged);
            throw new ConflictException(
                "Only a completed payment can be refunded.",
                ErrorCodes.ConcurrencyConflict);
        }

        if (payment.RefundedAmount + amount > payment.Amount)
        {
            _idempotency.Detach(staged);
            throw new BadRequestException("Refund exceeds the remaining refundable amount.");
        }

        var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            var rows = await _context.TryRefundPaymentAsync(payment.Id, amount, cancellationToken);
            if (rows != 1)
            {
                await RollbackAsync(transaction);
                transaction = null;
                _idempotency.Detach(staged);

                var replay = await _idempotency.BeginLocalAsync(
                    IdempotencyOperations.PaymentRefund,
                    request.IdempotencyKey,
                    hash,
                    cancellationToken);
                if (replay.Kind == IdempotencyOutcomeKind.Replay)
                    return replay.ResourceId ?? payment.Id;
                if (replay.Record is not null && replay.Kind == IdempotencyOutcomeKind.Started)
                    _idempotency.Detach(replay.Record);

                throw new ConflictException(
                    "The payment changed while the refund was being saved. Refresh and try again.",
                    ErrorCodes.ConcurrencyConflict);
            }

            _idempotency.CompleteLocal(
                staged,
                200,
                JsonSerializer.Serialize(new { id = payment.Id, refundedAmount = payment.RefundedAmount + amount }),
                payment.Id);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbExceptionClassifier.IsUniqueViolation(ex))
        {
            await RollbackAsync(transaction);
            transaction = null;
            _idempotency.Detach(staged);
            var replay = await _idempotency.BeginLocalAsync(
                IdempotencyOperations.PaymentRefund,
                request.IdempotencyKey,
                hash,
                cancellationToken);
            if (replay.Kind == IdempotencyOutcomeKind.Replay && replay.ResourceId.HasValue)
                return replay.ResourceId.Value;

            throw new ConflictException(
                "This Idempotency-Key was already used for a different refund request.",
                ErrorCodes.IdempotencyKeyReused);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }

        try
        {
            await InvoicePaymentCalculator.ReconcileOrdersAsync(_context, [payment.OrderId], cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invoice balance reconcile failed for order {OrderId}", payment.OrderId);
        }

        await EntityCacheInvalidator.InvalidatePaymentAsync(_cache, tenantId, payment.Id, cancellationToken);
        _logger.LogInformation(
            "Refunded {Amount} {Currency} on payment {PaymentId} in tenant {TenantId}",
            amount,
            currency,
            payment.Id,
            tenantId);

        return payment.Id;
    }

    private static async Task RollbackAsync(IApplicationTransaction? transaction)
    {
        if (transaction is null)
            return;

        await transaction.RollbackAsync();
        await transaction.DisposeAsync();
    }
}

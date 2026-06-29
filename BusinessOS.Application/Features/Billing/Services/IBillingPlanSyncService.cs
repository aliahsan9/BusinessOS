using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Billing.Services;

public interface IBillingPlanSyncService
{
    Task ApplyPlanFromWebhookAsync(
        Guid tenantId,
        Guid planId,
        SubscriptionStatus status,
        BillingInterval interval,
        PaymentProviderType provider,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        DateTime? periodEnd,
        CancellationToken cancellationToken = default);

    Task RecordTransactionAsync(
        Guid tenantId,
        decimal amount,
        string currency,
        BillingTransactionStatus status,
        PaymentProviderType provider,
        string transactionId,
        string? description,
        Guid? invoiceId,
        CancellationToken cancellationToken = default);
}

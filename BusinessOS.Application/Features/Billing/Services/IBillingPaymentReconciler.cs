using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Billing.Services;

public interface IBillingPaymentReconciler
{
    /// <summary>
    /// Applies a verified provider result to the billing transaction identified by the stable operation key.
    /// Returns false when no local transaction exists. Does not create a charge from webhook metadata.
    /// </summary>
    Task<bool> ApplyVerifiedResultAsync(
        PaymentProviderType provider,
        string operationKey,
        BillingTransactionStatus status,
        string? providerReference,
        decimal? amount,
        string? currency,
        CancellationToken cancellationToken = default);
}

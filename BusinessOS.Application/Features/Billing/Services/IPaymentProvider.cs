using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Application.Features.Billing.Services;

public sealed record ProviderCheckoutCommand(
    Guid TenantId,
    string TenantName,
    string Email,
    SubscriptionPlanDto Plan,
    string BillingInterval,
    decimal Amount,
    string Currency,
    string? SuccessUrl,
    string? CancelUrl,
    string? ExistingCustomerId,
    string? OperationKey);

public interface IPaymentProvider
{
    PaymentProviderType ProviderType { get; }
    string Name { get; }
    bool IsConfigured { get; }
    bool SupportsRecurring { get; }
    IReadOnlySet<string> ChargeableCurrencies { get; }

    bool CanCharge(string currency);

    Task<CheckoutSessionDto> CreateSubscriptionCheckoutAsync(
        ProviderCheckoutCommand command,
        CancellationToken cancellationToken = default);
}

public interface IPaymentProviderSelector
{
    IReadOnlyList<IPaymentProvider> Providers { get; }

    IPaymentProvider Require(string? requestedProvider, string currency);
}

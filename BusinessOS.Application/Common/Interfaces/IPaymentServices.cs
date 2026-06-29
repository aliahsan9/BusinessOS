using BusinessOS.Application.Features.Billing.DTOs;

namespace BusinessOS.Application.Common.Interfaces;

public interface IStripePaymentService
{
    Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
        Guid tenantId,
        string tenantName,
        string email,
        SubscriptionPlanDto plan,
        string billingInterval,
        string? successUrl,
        string? cancelUrl,
        string? existingCustomerId,
        CancellationToken cancellationToken = default);

    Task<BillingPortalDto> CreateBillingPortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default);

    bool IsConfigured { get; }
}

public interface IJazzCashPaymentService
{
    Task<CheckoutSessionDto> InitiatePaymentAsync(
        Guid tenantId,
        SubscriptionPlanDto plan,
        string billingInterval,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyPaymentAsync(string transactionReference, CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(string payload, CancellationToken cancellationToken = default);

    bool IsConfigured { get; }
}

public interface IEasyPaisaPaymentService
{
    Task<CheckoutSessionDto> InitiatePaymentAsync(
        Guid tenantId,
        SubscriptionPlanDto plan,
        string billingInterval,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyPaymentAsync(string transactionReference, CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(string payload, CancellationToken cancellationToken = default);

    bool IsConfigured { get; }
}

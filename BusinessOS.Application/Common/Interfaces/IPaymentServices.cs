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
        CancellationToken cancellationToken = default,
        string? operationKey = null,
        string? currency = null);

    Task<BillingPortalDto> CreateBillingPortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default);

    Task SetCancelAtPeriodEndAsync(
        string stripeSubscriptionId,
        bool cancelAtPeriodEnd,
        CancellationToken cancellationToken = default);

    Task CancelSubscriptionNowAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken = default);

    Task<StripeSubscriptionChange> ChangeSubscriptionPriceAsync(
        string stripeSubscriptionId,
        Guid tenantId,
        Guid planId,
        string planName,
        string billingInterval,
        decimal recurringAmount,
        string currency,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    bool IsConfigured { get; }
}

public sealed record StripeSubscriptionChange(
    bool Confirmed,
    string Status,
    DateTime? PeriodEnd,
    decimal AmountDue,
    string Currency,
    string? InvoiceId);

public interface IJazzCashPaymentService
{
    Task<CheckoutSessionDto> InitiatePaymentAsync(
        Guid tenantId,
        SubscriptionPlanDto plan,
        string billingInterval,
        CancellationToken cancellationToken = default,
        string? operationKey = null);

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
        CancellationToken cancellationToken = default,
        string? operationKey = null);

    Task<bool> VerifyPaymentAsync(string transactionReference, CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(string payload, CancellationToken cancellationToken = default);

    bool IsConfigured { get; }
}

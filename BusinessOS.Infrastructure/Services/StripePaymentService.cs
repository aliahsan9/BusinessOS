using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace BusinessOS.Infrastructure.Services;

public sealed class StripePaymentService : IStripePaymentService
{
    private readonly StripeOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        IOptions<StripeOptions> options,
        IServiceProvider serviceProvider,
        ILogger<StripePaymentService> logger)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;

        if (IsConfigured)
        {
            StripeConfiguration.ApiKey = _options.SecretKey;
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SecretKey);

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(
        Guid tenantId,
        string tenantName,
        string email,
        SubscriptionPlanDto plan,
        string billingInterval,
        string? successUrl,
        string? cancelUrl,
        string? existingCustomerId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Stripe is not configured. Returning sandbox checkout placeholder.");
            return new CheckoutSessionDto
            {
                SessionId = $"sandbox_{Guid.NewGuid():N}",
                CheckoutUrl = null,
                Provider = "stripe"
            };
        }

        var isYearly = billingInterval.Equals("yearly", StringComparison.OrdinalIgnoreCase);
        var price = isYearly ? plan.AnnualPrice : plan.MonthlyPrice;

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = successUrl ?? _options.SuccessUrl,
            CancelUrl = cancelUrl ?? _options.CancelUrl,
            CustomerEmail = string.IsNullOrWhiteSpace(existingCustomerId) ? email : null,
            Customer = existingCustomerId,
            ClientReferenceId = tenantId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["tenant_id"] = tenantId.ToString(),
                ["plan_id"] = plan.Id.ToString(),
                ["billing_interval"] = isYearly ? "yearly" : "monthly"
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = (long)(price * 100),
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = isYearly ? "year" : "month"
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"BusinessOS {plan.Name}",
                            Description = plan.Description ?? $"{plan.Name} subscription"
                        }
                    },
                    Quantity = 1
                }
            ]
        };

        var service = new SessionService();
        var session = await service.CreateAsync(sessionOptions, cancellationToken: cancellationToken);

        return new CheckoutSessionDto
        {
            SessionId = session.Id,
            CheckoutUrl = session.Url,
            Provider = "stripe"
        };
    }

    public async Task<BillingPortalDto> CreateBillingPortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Stripe is not configured.");
        }

        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        }, cancellationToken: cancellationToken);

        return new BillingPortalDto { PortalUrl = session.Url };
    }

    public async Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            _logger.LogWarning("Stripe webhook received but Stripe is not fully configured.");
            return;
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid Stripe webhook signature.");
            throw;
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutCompletedAsync(stripeEvent, cancellationToken);
                break;
            case EventTypes.CustomerSubscriptionCreated:
            case EventTypes.CustomerSubscriptionUpdated:
                await HandleSubscriptionUpdatedAsync(stripeEvent, cancellationToken);
                break;
            case EventTypes.CustomerSubscriptionDeleted:
                await HandleSubscriptionDeletedAsync(stripeEvent, cancellationToken);
                break;
            case EventTypes.InvoicePaid:
                await HandleInvoicePaidAsync(stripeEvent, cancellationToken);
                break;
            case EventTypes.InvoicePaymentFailed:
                await HandlePaymentFailedAsync(stripeEvent, cancellationToken);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session?.Metadata is null || !session.Metadata.TryGetValue("tenant_id", out var tenantIdStr))
        {
            return;
        }

        if (!Guid.TryParse(tenantIdStr, out var tenantId) ||
            !session.Metadata.TryGetValue("plan_id", out var planIdStr) ||
            !Guid.TryParse(planIdStr, out var planId))
        {
            return;
        }

        var interval = session.Metadata.GetValueOrDefault("billing_interval", "monthly");
        var billingInterval = interval == "yearly" ? BillingInterval.Yearly : BillingInterval.Monthly;

        var planSync = _serviceProvider.GetRequiredService<IBillingPlanSyncService>();
        await planSync.ApplyPlanFromWebhookAsync(
            tenantId,
            planId,
            SubscriptionStatus.Active,
            billingInterval,
            PaymentProviderType.Stripe,
            session.CustomerId,
            session.SubscriptionId,
            DateTime.UtcNow.AddMonths(billingInterval == BillingInterval.Yearly ? 12 : 1),
            cancellationToken);

        var amount = session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : 0;
        await planSync.RecordTransactionAsync(
            tenantId,
            amount,
            session.Currency?.ToUpperInvariant() ?? "USD",
            BillingTransactionStatus.Completed,
            PaymentProviderType.Stripe,
            session.Id,
            "Stripe checkout completed",
            null,
            cancellationToken);
    }

    private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription?.Metadata is null || !subscription.Metadata.TryGetValue("tenant_id", out var tenantIdStr))
        {
            return;
        }

        if (!Guid.TryParse(tenantIdStr, out var tenantId))
        {
            return;
        }

        var status = subscription.Status switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trial,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Cancelled,
            _ => SubscriptionStatus.Active
        };

        var periodEnd = subscription.CancelAt ?? subscription.EndedAt ?? DateTime.UtcNow.AddMonths(1);

        if (subscription.Metadata.TryGetValue("plan_id", out var planIdStr) && Guid.TryParse(planIdStr, out var planId))
        {
            await _serviceProvider.GetRequiredService<IBillingPlanSyncService>().ApplyPlanFromWebhookAsync(
                tenantId,
                planId,
                status,
                BillingInterval.Monthly,
                PaymentProviderType.Stripe,
                subscription.CustomerId,
                subscription.Id,
                periodEnd,
                cancellationToken);
        }
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription?.Metadata is null || !subscription.Metadata.TryGetValue("tenant_id", out var tenantIdStr))
        {
            return;
        }

        if (!Guid.TryParse(tenantIdStr, out var tenantId) ||
            !subscription.Metadata.TryGetValue("plan_id", out var planIdStr) ||
            !Guid.TryParse(planIdStr, out var planId))
        {
            return;
        }

        await _serviceProvider.GetRequiredService<IBillingPlanSyncService>().ApplyPlanFromWebhookAsync(
            tenantId,
            planId,
            SubscriptionStatus.Cancelled,
            BillingInterval.Monthly,
            PaymentProviderType.Stripe,
            subscription.CustomerId,
            subscription.Id,
            null,
            cancellationToken);
    }

    private async Task HandleInvoicePaidAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice?.CustomerId is null)
        {
            return;
        }

        _logger.LogInformation("Stripe invoice paid: {InvoiceId}", invoice.Id);
    }

    private async Task HandlePaymentFailedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        _logger.LogWarning("Stripe payment failed for invoice {InvoiceId}", invoice?.Id);
    }
}

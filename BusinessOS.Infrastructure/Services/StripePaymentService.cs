using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
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
        CancellationToken cancellationToken = default,
        string? operationKey = null,
        string? currency = null)
    {
        var chargeCurrency = string.IsNullOrWhiteSpace(currency) ? "usd" : currency.Trim().ToLowerInvariant();
        if (!IsConfigured)
        {
            _logger.LogWarning("Stripe is not configured. Checkout requires server confirmation and does not activate the subscription.");
            return new CheckoutSessionDto
            {
                SessionId = string.IsNullOrWhiteSpace(operationKey) ? $"sandbox_{Guid.NewGuid():N}" : operationKey,
                CheckoutUrl = null,
                Provider = "stripe",
                RequiresServerConfirmation = true,
                Currency = chargeCurrency.ToUpperInvariant()
            };
        }

        var isYearly = billingInterval.Equals("yearly", StringComparison.OrdinalIgnoreCase)
            || billingInterval.Equals("annual", StringComparison.OrdinalIgnoreCase);
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
                ["billing_interval"] = isYearly ? "yearly" : "monthly",
                ["operation_key"] = operationKey ?? string.Empty
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["tenant_id"] = tenantId.ToString(),
                    ["plan_id"] = plan.Id.ToString(),
                    ["billing_interval"] = isYearly ? "yearly" : "monthly"
                }
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = chargeCurrency,
                        UnitAmount = CurrencyMath.ToMinorUnits(price, chargeCurrency),
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
        var requestOptions = new RequestOptions();
        if (!string.IsNullOrWhiteSpace(operationKey))
            requestOptions.IdempotencyKey = operationKey;

        var session = await service.CreateAsync(sessionOptions, requestOptions, cancellationToken);

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

    public async Task SetCancelAtPeriodEndAsync(
        string stripeSubscriptionId,
        bool cancelAtPeriodEnd,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Stripe is not configured.");

        try
        {
            var service = new SubscriptionService();
            await service.UpdateAsync(
                stripeSubscriptionId,
                new SubscriptionUpdateOptions { CancelAtPeriodEnd = cancelAtPeriodEnd },
                cancellationToken: cancellationToken);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(
                "Stripe declined a cancellation change for {SubscriptionId}. Code {Code}",
                stripeSubscriptionId,
                ex.StripeError?.Code);
            throw new BadRequestException(ex.StripeError?.Message ?? "Stripe could not update the subscription.");
        }
    }

    public async Task CancelSubscriptionNowAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Stripe is not configured.");

        try
        {
            var service = new SubscriptionService();
            await service.CancelAsync(stripeSubscriptionId, cancellationToken: cancellationToken);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(
                "Stripe declined an immediate cancellation for {SubscriptionId}. Code {Code}",
                stripeSubscriptionId,
                ex.StripeError?.Code);
            throw new BadRequestException(ex.StripeError?.Message ?? "Stripe could not cancel the subscription.");
        }
    }

    public async Task<StripeSubscriptionChange> ChangeSubscriptionPriceAsync(
        string stripeSubscriptionId,
        Guid tenantId,
        Guid planId,
        string planName,
        string billingInterval,
        decimal recurringAmount,
        string currency,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Stripe is not configured.");

        var isYearly = billingInterval.Equals("yearly", StringComparison.OrdinalIgnoreCase)
            || billingInterval.Equals("annual", StringComparison.OrdinalIgnoreCase);
        var service = new SubscriptionService();
        var current = await service.GetAsync(stripeSubscriptionId, cancellationToken: cancellationToken);
        var item = current.Items?.Data?.FirstOrDefault()
            ?? throw new BadRequestException("The Stripe subscription has no billable item.");
        var productId = item.Price?.ProductId;
        if (string.IsNullOrWhiteSpace(productId))
            throw new BadRequestException("The Stripe subscription price is not linked to a product.");

        Subscription updated;
        try
        {
            updated = await service.UpdateAsync(
                stripeSubscriptionId,
                new SubscriptionUpdateOptions
                {
                    PaymentBehavior = "error_if_incomplete",
                    ProrationBehavior = "always_invoice",
                    Metadata = new Dictionary<string, string>
                    {
                        ["tenant_id"] = tenantId.ToString(),
                        ["plan_id"] = planId.ToString(),
                        ["billing_interval"] = isYearly ? "yearly" : "monthly"
                    },
                    Items =
                    [
                        new SubscriptionItemOptions
                        {
                            Id = item.Id,
                            Quantity = 1,
                            PriceData = new SubscriptionItemPriceDataOptions
                            {
                                Currency = currency.ToLowerInvariant(),
                                UnitAmount = CurrencyMath.ToMinorUnits(recurringAmount, currency),
                                Product = productId,
                                Recurring = new SubscriptionItemPriceDataRecurringOptions
                                {
                                    Interval = isYearly ? "year" : "month"
                                }
                            }
                        }
                    ],
                    Expand = ["latest_invoice"]
                },
                string.IsNullOrWhiteSpace(idempotencyKey)
                    ? null
                    : new RequestOptions { IdempotencyKey = $"sub_{idempotencyKey}" },
                cancellationToken);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(
                "Stripe declined a subscription change for {SubscriptionId}. Code {Code}",
                stripeSubscriptionId,
                ex.StripeError?.Code);
            throw new BadRequestException(ex.StripeError?.Message ?? "Stripe could not change the subscription.");
        }

        var invoice = updated.LatestInvoice;
        var chargeCurrency = (invoice?.Currency ?? currency).ToUpperInvariant();
        var due = invoice?.AmountDue is long minor
            ? CurrencyMath.FromMinorUnits(minor, chargeCurrency)
            : recurringAmount;
        var periodEnd = updated.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        var confirmed = updated.Status is "active" or "trialing";
        return new StripeSubscriptionChange(
            confirmed,
            updated.Status ?? "pending",
            periodEnd == default ? null : periodEnd,
            due,
            chargeCurrency,
            invoice?.Id);
    }

    public async Task HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            _logger.LogWarning("Rejected Stripe webhook because the webhook secret is not configured.");
            throw new UnauthorizedException("Stripe webhook secret is not configured.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);
        }
        catch (Exception)
        {
            _logger.LogWarning("Rejected Stripe webhook because the signature was invalid.");
            throw new UnauthorizedException("Invalid Stripe webhook signature.");
        }

        if (string.IsNullOrWhiteSpace(stripeEvent.Id))
            throw new BadRequestException("Stripe webhook event id is missing.");

        var store = _serviceProvider.GetRequiredService<IWebhookEventStore>();
        var claim = await store.TryBeginAsync(PaymentProviderType.Stripe, stripeEvent.Id, cancellationToken);
        if (claim is WebhookClaim.Duplicate)
        {
            _logger.LogInformation("Ignored duplicate Stripe webhook {EventId}", stripeEvent.Id);
            return;
        }

        if (claim is WebhookClaim.Busy)
        {
            throw new ConflictException(
                "Webhook event is already being processed.",
                ErrorCodes.IdempotencyInProgress);
        }

        var started = (WebhookClaim.Started)claim;
        try
        {
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

            await store.CompleteAsync(started.EventRecordId, null, cancellationToken);
        }
        catch
        {
            await store.AbandonAsync(started.EventRecordId, cancellationToken);
            throw;
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
        var operationKey = session.Metadata.GetValueOrDefault("operation_key");
        var currency = session.Currency?.ToUpperInvariant() ?? "USD";
        var amount = session.AmountTotal.HasValue
            ? CurrencyMath.FromMinorUnits(session.AmountTotal.Value, currency)
            : 0m;

        if (!string.IsNullOrWhiteSpace(operationKey))
        {
            var reconciler = _serviceProvider.GetRequiredService<IBillingPaymentReconciler>();
            var operationTenantId = await reconciler.ApplyVerifiedResultAsync(
                PaymentProviderType.Stripe,
                operationKey,
                BillingTransactionStatus.Completed,
                session.Id,
                amount,
                currency,
                cancellationToken);

            if (operationTenantId is null || operationTenantId.Value != tenantId)
            {
                _logger.LogWarning(
                    "Stripe checkout {SessionId} did not match tenant {TenantId} for operation {OperationKey}",
                    session.Id,
                    tenantId,
                    operationKey);
                return;
            }
        }

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

        await planSync.RecordTransactionAsync(
            tenantId,
            amount,
            currency,
            BillingTransactionStatus.Completed,
            PaymentProviderType.Stripe,
            string.IsNullOrWhiteSpace(operationKey) ? session.Id : operationKey,
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
            "incomplete" => SubscriptionStatus.Incomplete,
            "incomplete_expired" => SubscriptionStatus.Expired,
            "unpaid" => SubscriptionStatus.PastDue,
            _ => SubscriptionStatus.PendingPayment
        };

        var intervalName = subscription.Metadata.GetValueOrDefault("billing_interval", "monthly");
        var billingInterval = intervalName.Equals("yearly", StringComparison.OrdinalIgnoreCase)
            ? BillingInterval.Yearly
            : BillingInterval.Monthly;
        var itemPeriodEnd = subscription.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        var periodEnd = itemPeriodEnd is DateTime end && end != default
            ? end
            : subscription.CancelAt ?? subscription.EndedAt ?? DateTime.UtcNow.AddMonths(billingInterval == BillingInterval.Yearly ? 12 : 1);

        if (subscription.Metadata.TryGetValue("plan_id", out var planIdStr) && Guid.TryParse(planIdStr, out var planId))
        {
            await _serviceProvider.GetRequiredService<IBillingPlanSyncService>().ApplyPlanFromWebhookAsync(
                tenantId,
                planId,
                status,
                billingInterval,
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

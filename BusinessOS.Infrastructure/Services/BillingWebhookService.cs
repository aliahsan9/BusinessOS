using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

public sealed class BillingWebhookService : IBillingWebhookService
{
    private readonly IStripePaymentService _stripeService;
    private readonly IJazzCashPaymentService _jazzCashService;
    private readonly IEasyPaisaPaymentService _easyPaisaService;
    private readonly IOptions<TapOptions> _tap;
    private readonly IOptions<PayTabsOptions> _payTabs;
    private readonly IServiceProvider _services;

    public BillingWebhookService(
        IStripePaymentService stripeService,
        IJazzCashPaymentService jazzCashService,
        IEasyPaisaPaymentService easyPaisaService,
        IOptions<TapOptions> tap,
        IOptions<PayTabsOptions> payTabs,
        IServiceProvider services)
    {
        _stripeService = stripeService;
        _jazzCashService = jazzCashService;
        _easyPaisaService = easyPaisaService;
        _tap = tap;
        _payTabs = payTabs;
        _services = services;
    }

    public Task HandleStripeWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default) =>
        _stripeService.HandleWebhookAsync(payload, signature, cancellationToken);

    public Task HandleJazzCashWebhookAsync(string payload, CancellationToken cancellationToken = default) =>
        _jazzCashService.HandleWebhookAsync(payload, cancellationToken);

    public Task HandleEasyPaisaWebhookAsync(string payload, CancellationToken cancellationToken = default) =>
        _easyPaisaService.HandleWebhookAsync(payload, cancellationToken);

    public async Task HandleTapWebhookAsync(string payload, string? signature, CancellationToken cancellationToken = default)
    {
        var secret = string.IsNullOrWhiteSpace(_tap.Value.WebhookSecret) ? _tap.Value.SecretKey : _tap.Value.WebhookSecret;
        if (!ProviderWebhookSignatures.TapMatches(payload, signature, secret))
            throw new UnauthorizedException("Invalid Tap webhook signature.");

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var status = root.TryGetProperty("status", out var statusValue) ? statusValue.GetString() : null;
        if (!string.Equals(status, "CAPTURED", StringComparison.OrdinalIgnoreCase))
            return;

        if (!TryReadMetadata(root, out var tenantId, out var planId))
            return;

        var sync = _services.GetRequiredService<IBillingPlanSyncService>();
        await sync.ApplyPlanFromWebhookAsync(
            tenantId,
            planId,
            SubscriptionStatus.Active,
            BillingInterval.Monthly,
            PaymentProviderType.Tap,
            null,
            root.TryGetProperty("id", out var id) ? id.GetString() : null,
            DateTime.UtcNow.AddMonths(1),
            cancellationToken);
    }

    public async Task HandlePayTabsWebhookAsync(string payload, string? signature, CancellationToken cancellationToken = default)
    {
        if (!ProviderWebhookSignatures.PayTabsMatches(payload, signature, _payTabs.Value.ServerKey))
            throw new UnauthorizedException("Invalid PayTabs webhook signature.");

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var responseStatus = root.TryGetProperty("payment_result", out var result) &&
                             result.TryGetProperty("response_status", out var statusValue)
            ? statusValue.GetString()
            : null;
        if (!string.Equals(responseStatus, "A", StringComparison.OrdinalIgnoreCase))
            return;

        if (!root.TryGetProperty("cart_id", out var cartValue))
            return;

        var cartId = cartValue.GetString();
        if (string.IsNullOrWhiteSpace(cartId))
            return;

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var transaction = await db.BillingTransactions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => !x.IsDeleted && (x.TransactionId == cartId || x.ProviderOperationKey == cartId),
                cancellationToken);
        if (transaction is null || string.IsNullOrWhiteSpace(transaction.Metadata))
            return;

        var session = JsonSerializer.Deserialize<CheckoutSessionDto>(transaction.Metadata);
        if (session is null || session.PlanId == Guid.Empty)
            return;

        var sync = scope.ServiceProvider.GetRequiredService<IBillingPlanSyncService>();
        await sync.ApplyPlanFromWebhookAsync(
            transaction.TenantId,
            session.PlanId,
            SubscriptionStatus.Active,
            session.BillingInterval.Equals("yearly", StringComparison.OrdinalIgnoreCase) ||
            session.BillingInterval.Equals("annual", StringComparison.OrdinalIgnoreCase)
                ? BillingInterval.Yearly
                : BillingInterval.Monthly,
            PaymentProviderType.PayTabs,
            null,
            root.TryGetProperty("tran_ref", out var reference) ? reference.GetString() : cartId,
            DateTime.UtcNow.AddMonths(1),
            cancellationToken);
    }

    private static bool TryReadMetadata(JsonElement root, out Guid tenantId, out Guid planId)
    {
        tenantId = Guid.Empty;
        planId = Guid.Empty;
        if (!root.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object)
            return false;

        return metadata.TryGetProperty("tenant_id", out var tenantValue) &&
               Guid.TryParse(tenantValue.GetString(), out tenantId) &&
               metadata.TryGetProperty("plan_id", out var planValue) &&
               Guid.TryParse(planValue.GetString(), out planId);
    }
}

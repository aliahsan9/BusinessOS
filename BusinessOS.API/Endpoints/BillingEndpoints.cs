using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/billing/plans", GetPlans)
            .WithTags("Billing")
            .WithName("GetBillingPlansPublic")
            .AllowAnonymous()
            .Produces<IReadOnlyList<SubscriptionPlanDto>>(StatusCodes.Status200OK)
            .RequireRateLimiting("api");

        var group = app.MapGroup("/billing")
            .WithTags("Billing")
            .RequireAuthorization();

        group.MapGet("/subscription", GetCurrentPlan)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("GetBillingSubscription")
            .Produces<CurrentPlanDto>(StatusCodes.Status200OK);

        group.MapGet("/current-plan", GetCurrentPlan)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("GetCurrentPlan")
            .Produces<CurrentPlanDto>(StatusCodes.Status200OK);

        group.MapGet("/usage", GetUsage)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("GetBillingUsage")
            .Produces<BillingUsageDto>(StatusCodes.Status200OK);

        group.MapGet("/dashboard", GetDashboard)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("GetBillingDashboard")
            .Produces<BillingDashboardDto>(StatusCodes.Status200OK);

        group.MapGet("/invoices", GetInvoices)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("GetBillingInvoices")
            .Produces<IReadOnlyList<BillingInvoiceDto>>(StatusCodes.Status200OK);

        group.MapGet("/transactions", GetTransactions)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("GetBillingTransactions")
            .Produces<IReadOnlyList<BillingTransactionDto>>(StatusCodes.Status200OK);

        group.MapGet("/providers", GetProviders)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("GetPaymentProviders")
            .Produces<IReadOnlyList<PaymentProviderDto>>(StatusCodes.Status200OK);

        group.MapPost("/change-plan", ChangePlan)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("ChangeBillingPlan")
            .Produces<PlanChangeResultDto>(StatusCodes.Status200OK);

        group.MapPost("/reactivate", Reactivate)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("ReactivateSubscription")
            .Produces<CurrentPlanDto>(StatusCodes.Status200OK);

        group.MapPost("/checkout/confirm", ConfirmCheckout)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("ConfirmSandboxCheckout")
            .Produces<CurrentPlanDto>(StatusCodes.Status200OK);

        group.MapGet("/subscription/history", GetHistory)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("GetSubscriptionHistory")
            .Produces<IReadOnlyList<SubscriptionHistoryDto>>(StatusCodes.Status200OK);

        group.MapGet("/payment-methods", GetPaymentMethods)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("GetBillingPaymentMethods")
            .Produces<IReadOnlyList<PaymentMethodCapabilityDto>>(StatusCodes.Status200OK);

        group.MapGet("/invoices/{id:guid}/download", DownloadInvoice)
            .RequirePermission(PermissionCodes.SubscriptionView)
            .WithName("DownloadBillingInvoice");

        group.MapPost("/upgrade", UpgradePlan)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("UpgradePlan")
            .Produces<CurrentPlanDto>(StatusCodes.Status200OK);

        group.MapPost("/downgrade", DowngradePlan)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("DowngradePlan")
            .Produces<CurrentPlanDto>(StatusCodes.Status200OK);

        group.MapPost("/cancel", CancelPlan)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("CancelPlan")
            .Produces<CurrentPlanDto>(StatusCodes.Status200OK);

        group.MapPost("/renew", RenewPlan)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("RenewPlan")
            .Produces<CurrentPlanDto>(StatusCodes.Status200OK);

        group.MapPost("/checkout", CreateCheckout)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("CreateCheckout")
            .WithDescription(
                "Starts a provider checkout. Requires Idempotency-Key. " +
                "Retries of the same plan, interval, provider, amount, and currency reuse the original session. " +
                "A different payload returns 409 IDEMPOTENCY_KEY_REUSED. " +
                "A provider timeout does not start a second charge; retry the same key.")
            .Produces<CheckoutSessionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/transactions/{id:guid}/refund", RefundTransaction)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("RefundBillingTransaction")
            .WithDescription(
                "Refunds a completed billing transaction for its full amount. Requires Idempotency-Key. " +
                "This updates the local ledger. A provider refund is not sent unless a later reconciliation confirms it.")
            .Produces<BillingTransactionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/payment-portal", CreatePortal)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("CreatePaymentPortal")
            .Produces<BillingPortalDto>(StatusCodes.Status200OK);

        group.MapPost("/portal", CreatePortal)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("CreateBillingPortal")
            .Produces<BillingPortalDto>(StatusCodes.Status200OK);

        group.MapPost("/validate-downgrade/{planId:guid}", ValidateDowngrade)
            .RequirePermission(PermissionCodes.SubscriptionManage)
            .WithName("ValidateDowngrade")
            .Produces<DowngradeValidationDto>(StatusCodes.Status200OK);

        MapBillingWebhooks(app);
    }

    /// <summary>
    /// Keeps the original payment-provider callback URLs. These are not a second API version.
    /// They are excluded from OpenAPI so they do not duplicate the version 1.0 webhook operations.
    /// </summary>
    public static void MapLegacyBillingWebhooks(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/billing/webhook/stripe", HandleStripeWebhook)
            .WithTags("Billing")
            .WithName("StripeWebhookLegacy")
            .DisableAntiforgery()
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapPost("/api/billing/webhook/jazzcash", HandleJazzCashWebhook)
            .WithTags("Billing")
            .WithName("JazzCashWebhookLegacy")
            .DisableAntiforgery()
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapPost("/api/billing/webhook/easypaisa", HandleEasyPaisaWebhook)
            .WithTags("Billing")
            .WithName("EasyPaisaWebhookLegacy")
            .DisableAntiforgery()
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    private static void MapBillingWebhooks(IEndpointRouteBuilder app)
    {
        app.MapPost("/billing/webhook/stripe", HandleStripeWebhook)
            .WithTags("Billing")
            .WithName("StripeWebhook")
            .DisableAntiforgery()
            .AllowAnonymous();

        app.MapPost("/billing/webhook/jazzcash", HandleJazzCashWebhook)
            .WithTags("Billing")
            .WithName("JazzCashWebhook")
            .DisableAntiforgery()
            .AllowAnonymous();

        app.MapPost("/billing/webhook/easypaisa", HandleEasyPaisaWebhook)
            .WithTags("Billing")
            .WithName("EasyPaisaWebhook")
            .DisableAntiforgery()
            .AllowAnonymous();

        app.MapPost("/billing/webhook/tap", HandleTapWebhook)
            .WithTags("Billing")
            .WithName("TapWebhook")
            .DisableAntiforgery()
            .AllowAnonymous();

        app.MapPost("/billing/webhook/paytabs", HandlePayTabsWebhook)
            .WithTags("Billing")
            .WithName("PayTabsWebhook")
            .DisableAntiforgery()
            .AllowAnonymous();
    }

    private static async Task<IResult> GetPlans(
        IBillingService billingService,
        string? currency,
        string? country,
        CancellationToken ct) =>
        Results.Ok(await billingService.GetPlansAsync(currency, country, ct));

    private static async Task<IResult> GetCurrentPlan(IBillingService billingService, CancellationToken ct) =>
        Results.Ok(await billingService.GetCurrentPlanAsync(ct));

    private static async Task<IResult> GetUsage(IBillingService billingService, CancellationToken ct) =>
        Results.Ok(await billingService.GetUsageAsync(ct));

    private static async Task<IResult> GetDashboard(IBillingService billingService, CancellationToken ct) =>
        Results.Ok(await billingService.GetDashboardAsync(ct));

    private static async Task<IResult> GetInvoices(IBillingService billingService, CancellationToken ct) =>
        Results.Ok(await billingService.GetInvoicesAsync(ct));

    private static async Task<IResult> GetTransactions(IBillingService billingService, CancellationToken ct) =>
        Results.Ok(await billingService.GetTransactionsAsync(ct));

    private static async Task<IResult> GetProviders(IBillingService billingService, CancellationToken ct) =>
        Results.Ok(await billingService.GetPaymentProvidersAsync(ct));

    private static async Task<IResult> UpgradePlan(
        UpgradePlanRequest request,
        IBillingService billingService,
        CancellationToken ct) =>
        Results.Ok(await billingService.UpgradePlanAsync(request, ct));

    private static async Task<IResult> DowngradePlan(
        DowngradePlanRequest request,
        IBillingService billingService,
        CancellationToken ct) =>
        Results.Ok(await billingService.DowngradePlanAsync(request, ct));

    private static async Task<IResult> CancelPlan(
        CancelPlanRequest request,
        IBillingService billingService,
        CancellationToken ct) =>
        Results.Ok(await billingService.CancelPlanAsync(request, ct));

    private static async Task<IResult> RenewPlan(
        RenewPlanRequest request,
        IBillingService billingService,
        CancellationToken ct) =>
        Results.Ok(await billingService.RenewPlanAsync(request, ct));

    private static async Task<IResult> CreateCheckout(
        CheckoutRequest request,
        HttpContext httpContext,
        IBillingService billingService,
        CancellationToken ct)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!IdempotencyKeyRules.IsValid(key))
        {
            throw new BadRequestException(
                "Idempotency-Key header is required and must be 1-128 visible ASCII characters without spaces.");
        }

        return Results.Ok(await billingService.CreateCheckoutSessionAsync(request, ct, key));
    }

    private static async Task<IResult> RefundTransaction(
        Guid id,
        BillingRefundRequest request,
        HttpContext httpContext,
        IBillingService billingService,
        CancellationToken ct)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!IdempotencyKeyRules.IsValid(key))
        {
            throw new BadRequestException(
                "Idempotency-Key header is required and must be 1-128 visible ASCII characters without spaces.");
        }

        return Results.Ok(await billingService.RefundTransactionAsync(id, request.Amount, request.Currency, key!, ct));
    }

    private static async Task<IResult> CreatePortal(
        BillingPortalRequest request,
        IBillingService billingService,
        CancellationToken ct) =>
        Results.Ok(await billingService.CreateBillingPortalSessionAsync(request.ReturnUrl, ct));

    private static async Task<IResult> ValidateDowngrade(
        Guid planId,
        IBillingService billingService,
        CancellationToken ct) =>
        Results.Ok(await billingService.ValidateDowngradeAsync(planId, ct));

    private static async Task<IResult> HandleStripeWebhook(
        HttpRequest request,
        IBillingWebhookService webhookService,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = request.Headers["Stripe-Signature"].ToString();
        await webhookService.HandleStripeWebhookAsync(payload, signature, ct);
        return Results.Ok();
    }

    private static async Task<IResult> HandleJazzCashWebhook(
        HttpRequest request,
        IBillingWebhookService webhookService,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        await webhookService.HandleJazzCashWebhookAsync(payload, ct);
        return Results.Ok();
    }

    private static async Task<IResult> ChangePlan(
        ChangePlanRequest request,
        HttpContext httpContext,
        IBillingService billingService,
        CancellationToken ct)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        return Results.Ok(await billingService.ChangePlanAsync(request, ct, key));
    }

    private static async Task<IResult> Reactivate(IBillingService billingService, CancellationToken ct) =>
        Results.Ok(await billingService.ReactivateAsync(ct));

    private static async Task<IResult> ConfirmCheckout(
        ConfirmCheckoutRequest request,
        IBillingService billingService,
        CancellationToken ct) =>
        Results.Ok(await billingService.ConfirmSandboxCheckoutAsync(request.SessionId, ct));

    private static async Task<IResult> GetHistory(IBillingService billingService, CancellationToken ct) =>
        Results.Ok(await billingService.GetHistoryAsync(ct));

    private static async Task<IResult> GetPaymentMethods(
        IBillingService billingService,
        string? currency,
        string? country,
        CancellationToken ct) =>
        Results.Ok(await billingService.GetPaymentMethodsAsync(currency, country, ct));

    private static async Task<IResult> DownloadInvoice(
        Guid id,
        IBillingService billingService,
        CancellationToken ct)
    {
        var bytes = await billingService.DownloadInvoiceAsync(id, ct);
        return Results.File(bytes, "application/pdf", $"invoice-{id:N}.pdf");
    }

    private static async Task<IResult> HandleEasyPaisaWebhook(
        HttpRequest request,
        IBillingWebhookService webhookService,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        await webhookService.HandleEasyPaisaWebhookAsync(payload, ct);
        return Results.Ok();
    }

    private static async Task<IResult> HandleTapWebhook(
        HttpRequest request,
        IBillingWebhookService webhookService,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = request.Headers["hashstring"].ToString();
        await webhookService.HandleTapWebhookAsync(payload, signature, ct);
        return Results.Ok();
    }

    private static async Task<IResult> HandlePayTabsWebhook(
        HttpRequest request,
        IBillingWebhookService webhookService,
        CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = request.Headers["Signature"].ToString();
        await webhookService.HandlePayTabsWebhookAsync(payload, signature, ct);
        return Results.Ok();
    }
}

public record BillingPortalRequest(string ReturnUrl);

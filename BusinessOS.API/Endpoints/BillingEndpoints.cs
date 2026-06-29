using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/billing/plans", GetPlans)
            .WithTags("Billing")
            .WithName("GetBillingPlansPublic")
            .AllowAnonymous()
            .Produces<IReadOnlyList<SubscriptionPlanDto>>(StatusCodes.Status200OK);

        var group = app.MapGroup("/api/billing")
            .WithTags("Billing")
            .RequireAuthorization();

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
            .Produces<CheckoutSessionDto>(StatusCodes.Status200OK);

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

    private static void MapBillingWebhooks(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/billing/webhook/stripe", HandleStripeWebhook)
            .WithTags("Billing")
            .WithName("StripeWebhook")
            .DisableAntiforgery()
            .AllowAnonymous();

        app.MapPost("/api/billing/webhook/jazzcash", HandleJazzCashWebhook)
            .WithTags("Billing")
            .WithName("JazzCashWebhook")
            .DisableAntiforgery()
            .AllowAnonymous();

        app.MapPost("/api/billing/webhook/easypaisa", HandleEasyPaisaWebhook)
            .WithTags("Billing")
            .WithName("EasyPaisaWebhook")
            .DisableAntiforgery()
            .AllowAnonymous();
    }

    private static async Task<IResult> GetPlans(IBillingService billingService, CancellationToken ct) =>
        Results.Ok(await billingService.GetPlansAsync(ct));

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
        IBillingService billingService,
        CancellationToken ct) =>
        Results.Ok(await billingService.CreateCheckoutSessionAsync(request, ct));

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
}

public record BillingPortalRequest(string ReturnUrl);

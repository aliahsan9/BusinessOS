using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Tenant.DTOs;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenant")
            .WithTags("Tenant")
            .RequireAuthorization();

        group.MapGet("", GetTenant)
            .RequirePermission(PermissionCodes.TenantView)
            .WithName("GetTenant")
            .Produces<TenantDto>(StatusCodes.Status200OK);

        group.MapPut("", UpdateTenant)
            .RequirePermission(PermissionCodes.TenantManage)
            .WithName("UpdateTenant")
            .Produces<TenantDto>(StatusCodes.Status200OK);

        group.MapGet("/usage", GetTenantUsage)
            .RequirePermission(PermissionCodes.TenantView)
            .WithName("GetTenantUsage")
            .Produces<TenantUsageDto>(StatusCodes.Status200OK);

        group.MapGet("/dashboard", GetTenantDashboard)
            .RequirePermission(PermissionCodes.TenantView)
            .WithName("GetTenantDashboard")
            .Produces<TenantDashboardDto>(StatusCodes.Status200OK);

        group.MapGet("/settings", GetTenantSettings)
            .RequirePermission(PermissionCodes.TenantView)
            .WithName("GetTenantSettings")
            .Produces<TenantSettingsDto>(StatusCodes.Status200OK);

        group.MapPut("/settings", UpdateTenantSettings)
            .RequirePermission(PermissionCodes.TenantManage)
            .WithName("UpdateTenantSettings")
            .Produces<TenantSettingsDto>(StatusCodes.Status200OK);
    }

    public static void MapBusinessRegistrationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/register-business", RegisterBusiness)
            .WithTags("Tenant")
            .WithName("RegisterBusiness")
            .WithSummary("Register a new business tenant")
            .Produces<BusinessOS.Application.Features.Auth.DTOs.AuthResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetTenant(
        ITenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.GetTenantAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateTenant(
        UpdateTenantRequest request,
        ITenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.UpdateTenantAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTenantUsage(
        ITenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.GetTenantUsageAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTenantDashboard(
        ITenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.GetTenantDashboardAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTenantSettings(
        ITenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.GetTenantSettingsAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateTenantSettings(
        UpdateTenantSettingsRequest request,
        ITenantService tenantService,
        CancellationToken cancellationToken)
    {
        var result = await tenantService.UpdateTenantSettingsAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> RegisterBusiness(
        RegisterBusinessRequest request,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(
            request.Email,
            request.Password,
            request.OwnerFirstName,
            request.OwnerLastName,
            request.BusinessName,
            cancellationToken,
            request.Timezone,
            request.Currency,
            request.Industry);

        return Results.Created("/api/auth/login", result);
    }
}

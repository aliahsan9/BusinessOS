using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Settings.DTOs;
using BusinessOS.Application.Features.Settings.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings")
            .WithTags("Settings")
            .RequireAuthorization();

        group.MapGet("", GetSettings)
            .RequirePermission(PermissionCodes.SettingsView)
            .WithName("GetSettings")
            .Produces<TenantSettingsDto>(StatusCodes.Status200OK);

        group.MapPut("", UpdateSettings)
            .RequirePermission(PermissionCodes.SettingsUpdate)
            .WithName("UpdateSettings")
            .Produces<TenantSettingsDto>(StatusCodes.Status200OK);

        group.MapGet("/business-profile", GetBusinessProfile)
            .RequirePermission(PermissionCodes.SettingsView)
            .WithName("GetBusinessProfile")
            .Produces<BusinessProfileDto>(StatusCodes.Status200OK);

        group.MapPut("/business-profile", UpdateBusinessProfile)
            .RequirePermission(PermissionCodes.SettingsUpdate)
            .WithName("UpdateBusinessProfile")
            .Produces<BusinessProfileDto>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetSettings(
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.GetSettingsAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateSettings(
        UpdateTenantSettingsRequest request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateSettingsAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetBusinessProfile(
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.GetBusinessProfileAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateBusinessProfile(
        UpdateBusinessProfileRequest request,
        ISettingsService settingsService,
        CancellationToken cancellationToken)
    {
        var result = await settingsService.UpdateBusinessProfileAsync(request, cancellationToken);
        return Results.Ok(result);
    }
}

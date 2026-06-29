using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Organization.DTOs;
using BusinessOS.Application.Features.Organization.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organization")
            .WithTags("Organization")
            .RequireAuthorization();

        group.MapGet("", GetOrganization)
            .RequirePermission(PermissionCodes.OrganizationView)
            .WithName("GetOrganization")
            .Produces<OrganizationDto>(StatusCodes.Status200OK);

        group.MapPut("", UpdateOrganization)
            .RequirePermission(PermissionCodes.OrganizationManage)
            .WithName("UpdateOrganization")
            .Produces<OrganizationDto>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetOrganization(
        IOrganizationService organizationService,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.GetOrganizationAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateOrganization(
        UpdateOrganizationRequest request,
        IOrganizationService organizationService,
        CancellationToken cancellationToken)
    {
        var result = await organizationService.UpdateOrganizationAsync(request, cancellationToken);
        return Results.Ok(result);
    }
}

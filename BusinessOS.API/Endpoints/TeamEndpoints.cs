using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Team.DTOs;
using BusinessOS.Application.Features.Team.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class TeamEndpoints
{
    public static void MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/team")
            .WithTags("Team")
            .RequireAuthorization();

        group.MapGet("", GetTeamDashboard)
            .RequirePermission(PermissionCodes.TeamView)
            .WithName("GetTeamDashboard")
            .Produces<TeamDashboardDto>(StatusCodes.Status200OK);

        group.MapGet("/members", GetTeamMembers)
            .RequirePermission(PermissionCodes.TeamView)
            .WithName("GetTeamMembers")
            .Produces<PagedResult<TeamMemberDto>>(StatusCodes.Status200OK);

        group.MapGet("/members/{id}", GetTeamMember)
            .RequirePermission(PermissionCodes.TeamView)
            .WithName("GetTeamMember")
            .Produces<TeamMemberDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/invite", InviteTeamMember)
            .RequirePermission(PermissionCodes.TeamInvite)
            .WithName("InviteTeamMember")
            .Produces<TeamInvitationDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/member/{id}", UpdateTeamMember)
            .RequirePermission(PermissionCodes.TeamManage)
            .WithName("UpdateTeamMember")
            .Produces<TeamMemberDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/member/{id}", RemoveTeamMember)
            .RequirePermission(PermissionCodes.TeamManage)
            .WithName("RemoveTeamMember")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/activity", GetTeamActivity)
            .RequirePermission(PermissionCodes.TeamView)
            .WithName("GetTeamActivity")
            .Produces<IReadOnlyList<TeamActivityDto>>(StatusCodes.Status200OK);

        group.MapGet("/invitations", GetPendingInvitations)
            .RequirePermission(PermissionCodes.TeamView)
            .WithName("GetPendingInvitations")
            .Produces<IReadOnlyList<TeamInvitationDto>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetTeamDashboard(
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        var result = await teamService.GetDashboardAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTeamMembers(
        string? search,
        string? status,
        string? role,
        int page,
        int pageSize,
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        var result = await teamService.GetMembersAsync(search, status, role, page, pageSize, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTeamMember(
        string id,
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        var result = await teamService.GetMemberByIdAsync(id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> InviteTeamMember(
        InviteTeamMemberRequest request,
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        var result = await teamService.InviteMemberAsync(request, cancellationToken);
        return Results.Created($"/api/team/invitations/{result.Id}", result);
    }

    private static async Task<IResult> UpdateTeamMember(
        string id,
        UpdateTeamMemberRequest request,
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        var result = await teamService.UpdateMemberAsync(id, request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> RemoveTeamMember(
        string id,
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        await teamService.RemoveMemberAsync(id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetTeamActivity(
        int limit,
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        var result = await teamService.GetTeamActivityAsync(limit <= 0 ? 20 : limit, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPendingInvitations(
        ITeamService teamService,
        CancellationToken cancellationToken)
    {
        var result = await teamService.GetPendingInvitationsAsync(cancellationToken);
        return Results.Ok(result);
    }
}

using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Roles.DTOs;
using BusinessOS.Application.Features.Roles.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// Role and permission management endpoints.
/// </summary>
public static class RoleEndpoints
{
    /// <summary>
    /// Maps RBAC endpoints for roles, permissions, and assignments.
    /// </summary>
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var roles = app.MapGroup("/api/roles")
            .WithTags("Roles & Permissions")
            .RequireAuthorization();

        roles.MapPost("", CreateRole)
            .RequirePermission(PermissionCodes.RoleCreate)
            .WithName("CreateRole")
            .WithSummary("Create a role")
            .WithDescription("Creates a new RBAC role. Requires Role.Create permission.")
            .Produces<RoleDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        roles.MapGet("", GetRoles)
            .RequirePermission(PermissionCodes.RoleView)
            .WithName("GetRoles")
            .WithSummary("List roles")
            .WithDescription("Returns all roles with assigned permission codes. Requires Role.View permission.")
            .Produces<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        roles.MapGet("/{id:guid}", GetRoleById)
            .RequirePermission(PermissionCodes.RoleView)
            .WithName("GetRoleById")
            .WithSummary("Get role by id")
            .WithDescription("Returns a single role and its permissions. Requires Role.View permission.")
            .Produces<RoleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        roles.MapPut("/{id:guid}", UpdateRole)
            .RequirePermission(PermissionCodes.RoleUpdate)
            .WithName("UpdateRole")
            .WithSummary("Update a role")
            .WithDescription("Updates role metadata. Requires Role.Update permission.")
            .Produces<RoleDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        roles.MapDelete("/{id:guid}", DeleteRole)
            .RequirePermission(PermissionCodes.RoleDelete)
            .WithName("DeleteRole")
            .WithSummary("Delete a role")
            .WithDescription("Deletes a custom role. System roles cannot be deleted. Requires Role.Delete permission.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        roles.MapPost("/{roleId:guid}/permissions", AssignPermission)
            .RequirePermission(PermissionCodes.RoleUpdate)
            .WithName("AssignRolePermission")
            .WithSummary("Assign permission to role")
            .WithDescription("Grants a permission to a role. Requires Role.Update permission.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        roles.MapDelete("/{roleId:guid}/permissions/{permissionId:guid}", RemovePermission)
            .RequirePermission(PermissionCodes.RoleUpdate)
            .WithName("RemoveRolePermission")
            .WithSummary("Remove permission from role")
            .WithDescription("Revokes a permission from a role. Requires Role.Update permission.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var permissions = app.MapGroup("/api/permissions")
            .WithTags("Roles & Permissions")
            .RequireAuthorization();

        permissions.MapGet("", GetPermissions)
            .RequirePermission(PermissionCodes.RoleView)
            .WithName("GetPermissions")
            .WithSummary("List permissions")
            .WithDescription("Returns the full permission catalog grouped by category. Requires Role.View permission.")
            .Produces<IReadOnlyList<PermissionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        permissions.MapGet("/{id:guid}", GetPermissionById)
            .RequirePermission(PermissionCodes.RoleView)
            .WithName("GetPermissionById")
            .WithSummary("Get permission by id")
            .WithDescription("Returns a single permission definition. Requires Role.View permission.")
            .Produces<PermissionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var users = app.MapGroup("/api/users")
            .WithTags("Roles & Permissions")
            .RequireAuthorization();

        users.MapPost("/{userId}/roles", AssignUserRole)
            .RequirePermission(PermissionCodes.UserUpdate)
            .WithName("AssignUserRole")
            .WithSummary("Assign role to user")
            .WithDescription("Assigns an RBAC role to a user and syncs Identity roles. Requires User.Update permission.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapDelete("/{userId}/roles/{roleId:guid}", RemoveUserRole)
            .RequirePermission(PermissionCodes.UserUpdate)
            .WithName("RemoveUserRole")
            .WithSummary("Remove role from user")
            .WithDescription("Removes an RBAC role from a user. Requires User.Update permission.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateRole(
        CreateRoleRequest request,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        var role = await roleService.CreateRoleAsync(request, cancellationToken);
        return Results.Created($"/api/roles/{role.Id}", role);
    }

    private static async Task<IResult> GetRoles(
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        var roles = await roleService.GetRolesAsync(cancellationToken);
        return Results.Ok(roles);
    }

    private static async Task<IResult> GetRoleById(
        Guid id,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        var role = await roleService.GetRoleByIdAsync(id, cancellationToken);
        return Results.Ok(role);
    }

    private static async Task<IResult> UpdateRole(
        Guid id,
        UpdateRoleRequest request,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        var role = await roleService.UpdateRoleAsync(id, request, cancellationToken);
        return Results.Ok(role);
    }

    private static async Task<IResult> DeleteRole(
        Guid id,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        await roleService.DeleteRoleAsync(id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AssignPermission(
        Guid roleId,
        AssignPermissionRequest request,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        await roleService.AssignPermissionAsync(roleId, request.PermissionId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RemovePermission(
        Guid roleId,
        Guid permissionId,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        await roleService.RemovePermissionAsync(roleId, permissionId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AssignUserRole(
        string userId,
        AssignUserRoleRequest request,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        await roleService.AssignUserRoleAsync(userId, request.RoleId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveUserRole(
        string userId,
        Guid roleId,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
        await roleService.RemoveUserRoleAsync(userId, roleId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetPermissions(
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetPermissionsAsync(cancellationToken);
        return Results.Ok(permissions);
    }

    private static async Task<IResult> GetPermissionById(
        Guid id,
        IPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        var permission = await permissionService.GetPermissionByIdAsync(id, cancellationToken);
        return Results.Ok(permission);
    }
}

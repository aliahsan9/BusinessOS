using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Users.DTOs;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("", GetUsers)
            .RequirePermission(PermissionCodes.UserView)
            .WithName("GetUsers")
            .Produces<PagedResult<UserSummaryResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{userId}", GetUserById)
            .RequirePermission(PermissionCodes.UserView)
            .WithName("GetUserById")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("", CreateUser)
            .RequirePermission(PermissionCodes.UserCreate)
            .WithName("CreateUser")
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{userId}", UpdateUser)
            .RequirePermission(PermissionCodes.UserUpdate)
            .WithName("UpdateUser")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{userId}/deactivate", DeactivateUser)
            .RequirePermission(PermissionCodes.UserUpdate)
            .WithName("DeactivateUser")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{userId}/activate", ActivateUser)
            .RequirePermission(PermissionCodes.UserUpdate)
            .WithName("ActivateUser")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{userId}/reset-password", ResetPassword)
            .RequirePermission(PermissionCodes.UserUpdate)
            .WithName("ResetUserPassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetUsers(
        string? search,
        int page,
        int pageSize,
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId
            ?? throw new Application.Common.Exceptions.BadRequestException("Tenant context is required.");

        var result = await identityService.GetUsersAsync(
            tenantId, search, page, pageSize, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetUserById(
        string userId,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var user = await identityService.GetUserByIdAsync(userId, cancellationToken);
        return Results.Ok(user);
    }

    private static async Task<IResult> CreateUser(
        CreateUserAdminRequest request,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var result = await identityService.CreateUserAsync(
            new CreateUserRequest(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.TenantId),
            cancellationToken);

        if (!result.Succeeded)
            return Results.BadRequest(new { errors = result.Errors });

        var user = await identityService.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null)
            return Results.Created($"/api/users/{request.Email}", new { email = request.Email });

        var userDetails = await identityService.GetUserByIdAsync(user.Id, cancellationToken);
        return Results.Created($"/api/users/{user.Id}", userDetails);
    }

    private static async Task<IResult> UpdateUser(
        string userId,
        UpdateUserRequest request,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var user = await identityService.UpdateUserAsync(userId, request, cancellationToken);
        return Results.Ok(user);
    }

    private static async Task<IResult> DeactivateUser(
        string userId,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        await identityService.DeactivateUserAsync(userId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ActivateUser(
        string userId,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        await identityService.ActivateUserAsync(userId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPassword(
        string userId,
        ResetPasswordRequest request,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var result = await identityService.ResetPasswordAsync(
            userId, request.NewPassword, cancellationToken);

        if (!result.Succeeded)
            return Results.BadRequest(new { errors = result.Errors });

        return Results.NoContent();
    }
}

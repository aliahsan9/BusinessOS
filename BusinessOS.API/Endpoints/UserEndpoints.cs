using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Users.DTOs;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization()
            .RequireRateLimiting("api");

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
            ?? throw new BadRequestException("Tenant context is required.");

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
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context is required.");

        var email = request.Email?.Trim() ?? string.Empty;
        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName))
        {
            throw new BadRequestException("Email, password, first name, and last name are required.");
        }

        if (request.Password.Length > 128)
        {
            throw new BadRequestException("Password must not exceed 128 characters.");
        }

        var result = await identityService.CreateUserAsync(
            new CreateUserRequest(
                email,
                request.Password,
                firstName,
                lastName,
                tenantId),
            cancellationToken);

        if (!result.Succeeded)
            throw new BadRequestException(string.Join(" ", result.Errors));

        var user = await identityService.FindByEmailAsync(email, cancellationToken);
        if (user is null)
            return Results.Created($"/api/v1/users/{email}", new { email });

        var userDetails = await identityService.GetUserByIdAsync(user.Id, cancellationToken);
        return Results.Created($"/api/v1/users/{user.Id}", userDetails);
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
        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < 8 ||
            request.NewPassword.Length > 128)
        {
            throw new BadRequestException("New password must be between 8 and 128 characters.");
        }

        var result = await identityService.ResetPasswordAsync(
            userId, request.NewPassword, cancellationToken);

        if (!result.Succeeded)
            throw new BadRequestException(string.Join(" ", result.Errors));

        return Results.NoContent();
    }
}

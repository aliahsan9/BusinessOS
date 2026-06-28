using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Users.DTOs;

namespace BusinessOS.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<UserAuthResult?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<bool> ValidatePasswordAsync(UserAuthResult user, string password, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRolesAsync(UserAuthResult user, CancellationToken cancellationToken);

    Task<IdentityOperationResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task AddToRoleAsync(UserAuthResult user, string role, CancellationToken cancellationToken);

    Task<PagedResult<UserSummaryResponse>> GetUsersAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<UserResponse> GetUserByIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<UserResponse> UpdateUserAsync(
        string userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateUserAsync(string userId, CancellationToken cancellationToken = default);

    Task ActivateUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> ResetPasswordAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public sealed record UserAuthResult(string Id, string Email, Guid TenantId);

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    Guid TenantId);

public sealed record IdentityOperationResult(bool Succeeded, IReadOnlyList<string> Errors);

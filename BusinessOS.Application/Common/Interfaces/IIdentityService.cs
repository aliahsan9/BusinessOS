namespace BusinessOS.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<UserAuthResult?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<bool> ValidatePasswordAsync(UserAuthResult user, string password, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRolesAsync(UserAuthResult user, CancellationToken cancellationToken);

    Task<IdentityOperationResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    Task AddToRoleAsync(UserAuthResult user, string role, CancellationToken cancellationToken);
}

public sealed record UserAuthResult(string Id, string Email, Guid TenantId);

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    Guid TenantId);

public sealed record IdentityOperationResult(bool Succeeded, IReadOnlyList<string> Errors);

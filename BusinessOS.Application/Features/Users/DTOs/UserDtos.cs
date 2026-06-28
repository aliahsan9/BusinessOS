namespace BusinessOS.Application.Features.Users.DTOs;

public class UserResponse
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public Guid TenantId { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
    public DateTime? CreatedAt { get; set; }
}

public class UserSummaryResponse
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
}

public record CreateUserAdminRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    Guid TenantId);

public record UpdateUserRequest(
    string Email,
    string FirstName,
    string LastName,
    bool IsActive);

public record ResetPasswordRequest(string NewPassword);

namespace BusinessOS.Application.Features.Account.DTOs;

public class AccountProfileResponse
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid TenantId { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
    public DateTime? JoinedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

public record UpdateAccountProfileRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? AvatarUrl);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

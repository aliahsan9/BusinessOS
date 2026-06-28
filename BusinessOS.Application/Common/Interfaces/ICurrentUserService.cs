namespace BusinessOS.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    string? Email { get; }

    Guid? TenantId { get; }

    IReadOnlyList<string> Roles { get; }

    IReadOnlyList<string> Permissions { get; }

    bool HasPermission(string permissionCode);
}

namespace BusinessOS.Application.Features.Roles.DTOs;

public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<string> Permissions);

public sealed record PermissionDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string Category);

public sealed record CreateRoleRequest(
    string Name,
    string? Description,
    bool IsActive = true);

public sealed record UpdateRoleRequest(
    string Name,
    string? Description,
    bool IsActive);

public sealed record AssignPermissionRequest(Guid PermissionId);

public sealed record AssignUserRoleRequest(Guid RoleId);

using BusinessOS.Application.Features.Roles.DTOs;

namespace BusinessOS.Application.Features.Roles.Services;

public interface IRoleService
{
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<RoleDto> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task DeleteRoleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<RoleDto> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AssignPermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);

    Task RemovePermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);

    Task AssignUserRoleAsync(string userId, Guid roleId, CancellationToken cancellationToken = default);

    Task RemoveUserRoleAsync(string userId, Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);
}

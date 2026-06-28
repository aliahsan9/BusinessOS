using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Common.Interfaces;

public interface IRoleRepository
{
    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<Role?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Role?> GetRoleByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<Role> CreateRoleAsync(Role role, CancellationToken cancellationToken = default);

    Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default);

    Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default);

    Task AssignPermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);

    Task RemovePermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);

    Task AssignRoleToUserAsync(string userId, Guid roleId, CancellationToken cancellationToken = default);

    Task RemoveRoleFromUserAsync(string userId, Guid roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUserRoleNamesAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUserPermissionCodesAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);
}

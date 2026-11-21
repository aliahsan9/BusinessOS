using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly BusinessOSDbContext _context;

    public RoleRepository(BusinessOSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        await _context.RbacRoles
            .AsNoTracking()
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .OrderByDescending(x => x.IsSystem)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<Role?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.RbacRoles
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Role?> GetRoleByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var roles = await _context.RbacRoles
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .Where(x => x.Name == name)
            .ToListAsync(cancellationToken);

        return roles.FirstOrDefault(x => x.IsSystem)
            ?? roles.FirstOrDefault();
    }

    public async Task<Role> CreateRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        _context.RbacRoles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task UpdateRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        _context.RbacRoles.Update(role);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        _context.RbacRoles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignPermissionAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.RbacRoles
            .FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken)
            ?? throw new InvalidOperationException($"Role '{roleId}' was not found.");

        var exists = await _context.RolePermissions
            .AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken);

        if (exists)
        {
            return;
        }

        _context.RolePermissions.Add(new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            TenantId = role.IsSystem ? null : role.TenantId
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemovePermissionAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _context.RolePermissions
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken);

        if (assignment is null)
        {
            return;
        }

        _context.RolePermissions.Remove(assignment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignRoleToUserAsync(
        string userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveUserTenantIdAsync(userId, cancellationToken);
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException($"User '{userId}' has no company context.");
        }

        var exists = await _context.RbacUserRoles
            .AnyAsync(
                x => x.UserId == userId && x.RoleId == roleId && x.TenantId == tenantId,
                cancellationToken);

        if (exists)
        {
            return;
        }

        _context.RbacUserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            TenantId = tenantId
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveRoleFromUserAsync(
        string userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveUserTenantIdAsync(userId, cancellationToken);

        var assignment = await _context.RbacUserRoles
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.RoleId == roleId && x.TenantId == tenantId,
                cancellationToken);

        if (assignment is null)
        {
            return;
        }

        _context.RbacUserRoles.Remove(assignment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetUserRoleNamesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveUserTenantIdAsync(userId, cancellationToken);
        return await _context.RbacUserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.TenantId == tenantId)
            .Select(x => x.Role.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionCodesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveUserTenantIdAsync(userId, cancellationToken);
        var rolePermissions = await _context.RbacUserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.TenantId == tenantId && x.Role.IsActive)
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => x.Permission.Code)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var temporary = await _context.TemporaryPermissionGrants
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.UserId == userId &&
                !x.IsRevoked &&
                !x.IsDeleted &&
                x.ExpiresAt > now)
            .Select(x => x.PermissionCode)
            .ToListAsync(cancellationToken);

        return rolePermissions
            .Concat(temporary)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<Role>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveUserTenantIdAsync(userId, cancellationToken);
        return await _context.RbacUserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.TenantId == tenantId)
            .Select(x => x.Role)
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    private async Task<Guid> ResolveUserTenantIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

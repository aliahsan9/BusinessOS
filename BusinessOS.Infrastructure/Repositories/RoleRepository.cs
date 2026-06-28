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
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<Role?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.RbacRoles
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Role?> GetRoleByNameAsync(string name, CancellationToken cancellationToken = default) =>
        await _context.RbacRoles
            .Include(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

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
        var exists = await _context.RolePermissions
            .AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken);

        if (exists)
        {
            return;
        }

        _context.RolePermissions.Add(new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId
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
        var exists = await _context.RbacUserRoles
            .AnyAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken);

        if (exists)
        {
            return;
        }

        _context.RbacUserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveRoleFromUserAsync(
        string userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _context.RbacUserRoles
            .FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken);

        if (assignment is null)
        {
            return;
        }

        _context.RbacUserRoles.Remove(assignment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetUserRoleNamesAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await _context.RbacUserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Role.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetUserPermissionCodesAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await _context.RbacUserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Role.IsActive)
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => x.Permission.Code)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Role>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await _context.RbacUserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Role)
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
}

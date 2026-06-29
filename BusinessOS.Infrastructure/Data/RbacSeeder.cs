using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Data;

public static class RbacSeeder
{
    public static async Task SeedAsync(
        BusinessOSDbContext context,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await SeedPermissionsAsync(context, logger, cancellationToken);
        var roleMap = await SeedRolesAsync(context, roleManager, logger, cancellationToken);
        await SeedRolePermissionsAsync(context, roleMap, logger, cancellationToken);
        await SyncIdentityUserRolesAsync(context, userManager, roleMap, cancellationToken);
        await MigrateAdminToOwnerAsync(context, userManager, roleMap, cancellationToken);
    }

    private static async Task SeedPermissionsAsync(
        BusinessOSDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existingCodes = await context.Permissions
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);

        var existingSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = false;

        foreach (var definition in PermissionCodes.All)
        {
            if (existingSet.Contains(definition.Code))
            {
                continue;
            }

            context.Permissions.Add(new Permission
            {
                Name = definition.Name,
                Code = definition.Code,
                Description = definition.Description,
                Category = definition.Category
            });

            added = true;
            logger.LogInformation("Seeded permission {Permission}", definition.Code);
        }

        if (added)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<Dictionary<string, Role>> SeedRolesAsync(
        BusinessOSDbContext context,
        RoleManager<ApplicationRole> roleManager,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var roleMap = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
                logger.LogInformation("Seeded identity role {Role}", roleName);
            }

            var role = await context.RbacRoles.FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);
            if (role is null)
            {
                role = new Role
                {
                    Name = roleName,
                    Description = GetDefaultRoleDescription(roleName),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.RbacRoles.Add(role);
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seeded RBAC role {Role}", roleName);
            }

            roleMap[roleName] = role;
        }

        return roleMap;
    }

    private static async Task SeedRolePermissionsAsync(
        BusinessOSDbContext context,
        Dictionary<string, Role> roleMap,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var permissions = await context.Permissions.ToListAsync(cancellationToken);
        var permissionByCode = permissions.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        var rolePermissionMap = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [RoleNames.Owner] = permissions.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase),
            [RoleNames.Admin] = PermissionCodes.BuildAdminPermissions(),
            [RoleNames.Manager] = PermissionCodes.ManagerPermissions,
            [RoleNames.Employee] = PermissionCodes.EmployeePermissions,
            [RoleNames.Accountant] = PermissionCodes.AccountantPermissions,
            [RoleNames.Sales] = PermissionCodes.SalesPermissions,
            [RoleNames.InventoryManager] = PermissionCodes.InventoryManagerPermissions,
            [RoleNames.Viewer] = PermissionCodes.ViewOnly
        };

        foreach (var (roleName, permissionCodes) in rolePermissionMap)
        {
            if (!roleMap.TryGetValue(roleName, out var role))
            {
                continue;
            }

            var existingPermissionIds = await context.RolePermissions
                .Where(x => x.RoleId == role.Id)
                .Select(x => x.PermissionId)
                .ToListAsync(cancellationToken);

            var existingSet = existingPermissionIds.ToHashSet();

            foreach (var code in permissionCodes)
            {
                if (!permissionByCode.TryGetValue(code, out var permission))
                {
                    continue;
                }

                if (existingSet.Contains(permission.Id))
                {
                    continue;
                }

                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id
                });

                logger.LogInformation("Assigned permission {Permission} to role {Role}", code, roleName);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SyncIdentityUserRolesAsync(
        BusinessOSDbContext context,
        UserManager<ApplicationUser> userManager,
        Dictionary<string, Role> roleMap,
        CancellationToken cancellationToken)
    {
        var users = await userManager.Users.ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var identityRoles = await userManager.GetRolesAsync(user);

            foreach (var roleName in identityRoles)
            {
                if (!roleMap.TryGetValue(roleName, out var role))
                {
                    continue;
                }

                var exists = await context.RbacUserRoles
                    .AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id, cancellationToken);

                if (!exists)
                {
                    context.RbacUserRoles.Add(new UserRole
                    {
                        UserId = user.Id,
                        RoleId = role.Id
                    });
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task MigrateAdminToOwnerAsync(
        BusinessOSDbContext context,
        UserManager<ApplicationUser> userManager,
        Dictionary<string, Role> roleMap,
        CancellationToken cancellationToken)
    {
        if (!roleMap.TryGetValue(RoleNames.Owner, out var ownerRole))
        {
            return;
        }

        var tenants = await context.Tenants
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            if (string.IsNullOrWhiteSpace(tenant.OwnerUserId))
            {
                continue;
            }

            var owner = await userManager.FindByIdAsync(tenant.OwnerUserId);
            if (owner is null)
            {
                continue;
            }

            var roles = await userManager.GetRolesAsync(owner);
            if (roles.Contains(RoleNames.Owner, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (roles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase))
            {
                await userManager.RemoveFromRoleAsync(owner, RoleNames.Admin);

                var adminRbac = roleMap.GetValueOrDefault(RoleNames.Admin);
                if (adminRbac is not null)
                {
                    var adminAssignment = await context.RbacUserRoles
                        .FirstOrDefaultAsync(x => x.UserId == owner.Id && x.RoleId == adminRbac.Id, cancellationToken);

                    if (adminAssignment is not null)
                    {
                        context.RbacUserRoles.Remove(adminAssignment);
                    }
                }
            }

            await userManager.AddToRoleAsync(owner, RoleNames.Owner);

            var exists = await context.RbacUserRoles
                .AnyAsync(x => x.UserId == owner.Id && x.RoleId == ownerRole.Id, cancellationToken);

            if (!exists)
            {
                context.RbacUserRoles.Add(new UserRole
                {
                    UserId = owner.Id,
                    RoleId = ownerRole.Id
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string GetDefaultRoleDescription(string roleName) =>
        roleName switch
        {
            RoleNames.Owner => "Full access including ownership transfer and subscription billing.",
            RoleNames.Admin => "Almost full access except subscription billing and ownership transfer.",
            RoleNames.Manager => "Manages customers, projects, tasks, reports, and analytics.",
            RoleNames.Employee => "Views and updates assigned projects and tasks.",
            RoleNames.Accountant => "Manages invoices, expenses, and financial analytics.",
            RoleNames.Sales => "Manages customers and orders.",
            RoleNames.InventoryManager => "Manages inventory and products.",
            RoleNames.Viewer => "Read-only access across modules.",
            _ => roleName
        };
}

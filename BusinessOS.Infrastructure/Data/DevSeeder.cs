using BusinessOS.Application.Common.Authorization;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Data;

public static class DevSeeder
{
    public const string DefaultEmail = "admin@businessos.local";
    public const string DefaultPassword = "Admin123!";
    private const string DefaultBusinessName = "BusinessOS Demo";

    public static async Task SeedAsync(
        BusinessOSDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await userManager.FindByEmailAsync(DefaultEmail) is not null)
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = DefaultBusinessName,
            BusinessType = "General",
            Email = DefaultEmail,
            Phone = string.Empty,
            Address = string.Empty,
            SubscriptionPlan = "Free",
            IsActive = true,
            OwnerUserId = string.Empty
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync(cancellationToken);

        var user = new ApplicationUser
        {
            UserName = DefaultEmail,
            Email = DefaultEmail,
            FirstName = "Admin",
            LastName = "User",
            TenantId = tenantId,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, DefaultPassword);
        if (!createResult.Succeeded)
        {
            logger.LogWarning(
                "Failed to seed development user: {Errors}",
                string.Join(", ", createResult.Errors.Select(x => x.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, RoleNames.Admin);

        var adminRole = await context.RbacRoles
            .FirstOrDefaultAsync(x => x.Name == RoleNames.Admin, cancellationToken);

        if (adminRole is not null)
        {
            context.RbacUserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = adminRole.Id
            });
        }

        tenant.OwnerUserId = user.Id;
        await context.SaveChangesAsync(cancellationToken);

        await ExpenseCategorySeeder.SeedForTenantAsync(context, tenantId, logger, cancellationToken);

        logger.LogInformation(
            "Seeded development account. Email: {Email}, Password: {Password}",
            DefaultEmail,
            DefaultPassword);
    }
}

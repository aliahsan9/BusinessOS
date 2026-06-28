using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Data;

public static class ExpenseCategorySeeder
{
    public static readonly string[] DefaultCategories =
    [
        "Rent",
        "Electricity",
        "Internet",
        "Transportation",
        "Salary",
        "Marketing",
        "Office Supplies",
        "Taxes",
        "Maintenance",
        "Miscellaneous"
    ];

    public static async Task SeedForTenantAsync(
        BusinessOSDbContext context,
        Guid tenantId,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var existingNames = await context.ExpenseCategories
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var existingSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = false;

        foreach (var name in DefaultCategories)
        {
            if (existingSet.Contains(name))
            {
                continue;
            }

            context.ExpenseCategories.Add(new ExpenseCategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Description = $"Default {name.ToLowerInvariant()} expense category",
                IsActive = true
            });

            added = true;
            logger?.LogInformation("Seeded expense category {Category} for tenant {TenantId}", name, tenantId);
        }

        if (added)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task SeedAllTenantsAsync(
        BusinessOSDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var tenantIds = await context.Tenants
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            await SeedForTenantAsync(context, tenantId, logger, cancellationToken);
        }
    }
}

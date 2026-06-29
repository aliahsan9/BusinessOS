using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Data;

public static class SubscriptionPlanSeeder
{
    public static readonly Guid FreePlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ProPlanId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid EnterprisePlanId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static async Task SeedAsync(
        BusinessOSDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var plans = new[]
        {
            new SubscriptionPlan
            {
                Id = FreePlanId,
                Name = "Free",
                Slug = "free",
                Description = "Basic features for small businesses",
                MonthlyPrice = 0,
                MaxUsers = 3,
                MaxCustomers = 50,
                MaxProjects = 5,
                MaxStorageMb = 512,
                MaxAiRequests = 50,
                IsActive = true
            },
            new SubscriptionPlan
            {
                Id = ProPlanId,
                Name = "Pro",
                Slug = "pro",
                Description = "Advanced features for growing teams",
                MonthlyPrice = 49,
                MaxUsers = 25,
                MaxCustomers = 500,
                MaxProjects = 50,
                MaxStorageMb = 5120,
                MaxAiRequests = 500,
                IsActive = true
            },
            new SubscriptionPlan
            {
                Id = EnterprisePlanId,
                Name = "Enterprise",
                Slug = "enterprise",
                Description = "Unlimited scale for large organizations",
                MonthlyPrice = 199,
                MaxUsers = 1000,
                MaxCustomers = 100000,
                MaxProjects = 10000,
                MaxStorageMb = 102400,
                MaxAiRequests = 10000,
                IsActive = true
            }
        };

        var existingSlugs = await context.SubscriptionPlans
            .Select(x => x.Slug)
            .ToListAsync(cancellationToken);

        var existingSet = existingSlugs.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = false;

        foreach (var plan in plans)
        {
            if (existingSet.Contains(plan.Slug))
            {
                continue;
            }

            context.SubscriptionPlans.Add(plan);
            added = true;
            logger.LogInformation("Seeded subscription plan {Plan}", plan.Name);
        }

        if (added)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

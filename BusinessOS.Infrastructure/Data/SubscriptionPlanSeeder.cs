using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Data;

public static class SubscriptionPlanSeeder
{
    public static readonly Guid FreePlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid StarterPlanId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ProfessionalPlanId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid EnterprisePlanId = Guid.Parse("44444444-4444-4444-4444-444444444444");

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
                Description = "Get started with basic business management",
                MonthlyPrice = 0,
                AnnualPrice = 0,
                MaxUsers = 1,
                MaxCustomers = 25,
                MaxProjects = 10,
                MaxTasks = 100,
                MaxStorageMb = 512,
                MaxAiRequests = 0,
                HasAdvancedAnalytics = false,
                HasPdfReports = false,
                HasAdvancedReports = false,
                HasAiAssistant = false,
                HasPrioritySupport = false,
                SortOrder = 0,
                IsActive = true
            },
            new SubscriptionPlan
            {
                Id = StarterPlanId,
                Name = "Starter",
                Slug = "starter",
                Description = "For growing small businesses",
                MonthlyPrice = 29,
                AnnualPrice = 290,
                MaxUsers = 5,
                MaxCustomers = 500,
                MaxProjects = 100,
                MaxTasks = 5000,
                MaxStorageMb = 5120,
                MaxAiRequests = 0,
                HasAdvancedAnalytics = true,
                HasPdfReports = true,
                HasAdvancedReports = false,
                HasAiAssistant = false,
                HasPrioritySupport = false,
                SortOrder = 1,
                IsActive = true
            },
            new SubscriptionPlan
            {
                Id = ProfessionalPlanId,
                Name = "Professional",
                Slug = "professional",
                Description = "Advanced tools for professional teams",
                MonthlyPrice = 79,
                AnnualPrice = 790,
                MaxUsers = 20,
                MaxCustomers = SubscriptionPlan.Unlimited,
                MaxProjects = SubscriptionPlan.Unlimited,
                MaxTasks = SubscriptionPlan.Unlimited,
                MaxStorageMb = 51200,
                MaxAiRequests = SubscriptionPlan.Unlimited,
                HasAdvancedAnalytics = true,
                HasPdfReports = true,
                HasAdvancedReports = true,
                HasAiAssistant = true,
                HasPrioritySupport = false,
                SortOrder = 2,
                IsActive = true
            },
            new SubscriptionPlan
            {
                Id = EnterprisePlanId,
                Name = "Enterprise",
                Slug = "enterprise",
                Description = "Unlimited scale with priority support",
                MonthlyPrice = 199,
                AnnualPrice = 1990,
                MaxUsers = SubscriptionPlan.Unlimited,
                MaxCustomers = SubscriptionPlan.Unlimited,
                MaxProjects = SubscriptionPlan.Unlimited,
                MaxTasks = SubscriptionPlan.Unlimited,
                MaxStorageMb = SubscriptionPlan.Unlimited,
                MaxAiRequests = SubscriptionPlan.Unlimited,
                HasAdvancedAnalytics = true,
                HasPdfReports = true,
                HasAdvancedReports = true,
                HasAiAssistant = true,
                HasPrioritySupport = true,
                SortOrder = 3,
                IsActive = true
            }
        };

        foreach (var plan in plans)
        {
            var existing = await context.SubscriptionPlans
                .FirstOrDefaultAsync(x => x.Slug == plan.Slug, cancellationToken);

            if (existing is null)
            {
                context.SubscriptionPlans.Add(plan);
                logger.LogInformation("Seeded subscription plan {Plan}", plan.Name);
                continue;
            }

            existing.Name = plan.Name;
            existing.Description = plan.Description;
            existing.MonthlyPrice = plan.MonthlyPrice;
            existing.AnnualPrice = plan.AnnualPrice;
            existing.MaxUsers = plan.MaxUsers;
            existing.MaxCustomers = plan.MaxCustomers;
            existing.MaxProjects = plan.MaxProjects;
            existing.MaxTasks = plan.MaxTasks;
            existing.MaxStorageMb = plan.MaxStorageMb;
            existing.MaxAiRequests = plan.MaxAiRequests;
            existing.HasAiAssistant = plan.HasAiAssistant;
            existing.HasAdvancedAnalytics = plan.HasAdvancedAnalytics;
            existing.HasPdfReports = plan.HasPdfReports;
            existing.HasAdvancedReports = plan.HasAdvancedReports;
            existing.HasPrioritySupport = plan.HasPrioritySupport;
            existing.SortOrder = plan.SortOrder;
            existing.IsActive = plan.IsActive;
        }

        await SeedPaymentProvidersAsync(context, logger, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPaymentProvidersAsync(
        BusinessOSDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var providers = new[]
        {
            new PaymentProvider
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ProviderType = Domain.Enums.PaymentProviderType.Stripe,
                Name = "Stripe",
                Description = "Credit/debit card payments via Stripe",
                IsEnabled = true,
                IsSandbox = true,
                ConfigurationKey = "Stripe",
                SortOrder = 0
            },
            new PaymentProvider
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                ProviderType = Domain.Enums.PaymentProviderType.JazzCash,
                Name = "JazzCash",
                Description = "Mobile wallet payments via JazzCash",
                IsEnabled = false,
                IsSandbox = true,
                ConfigurationKey = "JazzCash",
                SortOrder = 1
            },
            new PaymentProvider
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                ProviderType = Domain.Enums.PaymentProviderType.EasyPaisa,
                Name = "EasyPaisa",
                Description = "Mobile wallet payments via EasyPaisa",
                IsEnabled = false,
                IsSandbox = true,
                ConfigurationKey = "EasyPaisa",
                SortOrder = 2
            }
        };

        foreach (var provider in providers)
        {
            if (!await context.PaymentProviders.AnyAsync(x => x.ProviderType == provider.ProviderType, cancellationToken))
            {
                context.PaymentProviders.Add(provider);
                logger.LogInformation("Seeded payment provider {Provider}", provider.Name);
            }
        }
    }
}

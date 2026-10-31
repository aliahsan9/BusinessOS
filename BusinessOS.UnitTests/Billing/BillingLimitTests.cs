using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.UnitTests.Billing;

public class BillingLimitTests
{
    [Fact]
    public async Task Product_limit_is_enforced_for_the_current_tenant()
    {
        var (context, tenantId, provider, factory) = Open();
        var plan = new SubscriptionPlan
        {
            Name = "Free",
            Slug = "free-limit",
            MonthlyPrice = 0,
            AnnualPrice = 0,
            MaxUsers = 1,
            MaxCustomers = 25,
            MaxProducts = 0,
            MaxProjects = 3,
            MaxTasks = 100,
            MaxAiRequests = 20,
            BaseCurrency = "USD",
            IsActive = true,
            IsPublic = true
        };
        context.SubscriptionPlans.Add(plan);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Limit Co",
            Slug = "limit-co",
            Email = "limit@example.com",
            Phone = "1",
            Address = "1",
            BusinessType = "Retail",
            SubscriptionPlanId = plan.Id,
            SubscriptionPlan = plan.Name,
            OwnerUserId = "owner"
        });
        context.SaveChanges();

        var service = new TenantLimitService(context, provider, factory);
        var act = () => service.EnsureWithinLimitAsync("products");
        await act.Should().ThrowAsync<PlanLimitExceededException>();
    }

    [Fact]
    public async Task Ai_quota_is_idempotent_for_the_same_operation()
    {
        var (context, tenantId, provider, factory) = Open();
        var plan = new SubscriptionPlan
        {
            Name = "Starter",
            Slug = "starter-ai",
            MonthlyPrice = 19,
            AnnualPrice = 182,
            MaxUsers = 3,
            MaxAiRequests = 1,
            MaxProducts = 10,
            BaseCurrency = "USD",
            IsActive = true,
            IsPublic = true
        };
        context.SubscriptionPlans.Add(plan);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "AI Co",
            Slug = "ai-co",
            Email = "ai@example.com",
            Phone = "1",
            Address = "1",
            BusinessType = "Retail",
            SubscriptionPlanId = plan.Id,
            SubscriptionPlan = plan.Name,
            OwnerUserId = "owner"
        });
        context.SaveChanges();

        var service = new TenantLimitService(context, provider, factory);
        await service.TryConsumeAiRequestAsync("same-key");
        await service.TryConsumeAiRequestAsync("same-key");

        var usage = context.TenantUsages.IgnoreQueryFilters().Single(x => x.TenantId == tenantId);
        usage.AiRequestsUsed.Should().Be(1);

        var again = () => service.TryConsumeAiRequestAsync("other-key");
        await again.Should().ThrowAsync<PlanLimitExceededException>();
    }

    [Fact]
    public async Task Expired_trial_moves_the_workspace_to_free_without_a_charge()
    {
        var (context, tenantId, _) = TenantDatabase.Open();
        var free = new SubscriptionPlan { Name = "Free", Slug = "free", MonthlyPrice = 0, AnnualPrice = 0, BaseCurrency = "USD", IsActive = true, IsPublic = true };
        var paid = new SubscriptionPlan { Name = "Starter", Slug = "starter", MonthlyPrice = 19, AnnualPrice = 182, BaseCurrency = "USD", IsActive = true, IsPublic = true };
        context.SubscriptionPlans.AddRange(free, paid);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Trial Co",
            Slug = "trial-co",
            Email = "trial@example.com",
            Phone = "1",
            Address = "1",
            BusinessType = "Retail",
            SubscriptionPlanId = paid.Id,
            SubscriptionPlan = paid.Name,
            OwnerUserId = "owner"
        });
        context.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = tenantId,
            SubscriptionPlanId = paid.Id,
            Status = Domain.Enums.SubscriptionStatus.Trial,
            TrialEndDate = DateTime.UtcNow.AddDays(-1)
        });
        context.SaveChanges();

        await SubscriptionMaintenance.ApplyDueTransitionsAsync(context, DateTime.UtcNow);

        var subscription = context.TenantSubscriptions.IgnoreQueryFilters().Single(x => x.TenantId == tenantId);
        subscription.Status.Should().Be(Domain.Enums.SubscriptionStatus.Expired);
        subscription.SubscriptionPlanId.Should().Be(free.Id);
        context.BillingTransactions.IgnoreQueryFilters().Should().BeEmpty();
    }

    private static (BusinessOSDbContext Context, Guid TenantId, TenantProvider Provider, IDbContextFactory<BusinessOSDbContext> Factory) Open()
    {
        var tenantId = Guid.NewGuid();
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return (
            new BusinessOSDbContext(options, provider),
            tenantId,
            provider,
            new NamedFactory(options, provider));
    }

    private sealed class NamedFactory : IDbContextFactory<BusinessOSDbContext>
    {
        private readonly DbContextOptions<BusinessOSDbContext> _options;
        private readonly TenantProvider _provider;

        public NamedFactory(DbContextOptions<BusinessOSDbContext> options, TenantProvider provider)
        {
            _options = options;
            _provider = provider;
        }

        public BusinessOSDbContext CreateDbContext() => new(_options, _provider);

        public Task<BusinessOSDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}

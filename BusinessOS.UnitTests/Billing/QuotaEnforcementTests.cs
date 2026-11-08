using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.UnitTests.Billing;

public class QuotaEnforcementTests
{
    [Fact]
    public async Task Users_AllowTheFirstThree_AndRejectTheFourth()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxUsers: 3);

        for (var i = 1; i <= 3; i++)
        {
            context.Users.Add(User(tenantId, $"member{i}"));
            await context.SaveChangesAsync();
        }

        context.Users.Add(User(tenantId, "member4"));
        var act = () => context.SaveChangesAsync();

        var error = await act.Should().ThrowAsync<PlanLimitExceededException>();
        error.Which.Resource.Should().Be("Users");
        error.Which.CurrentUsage.Should().Be(3);
        error.Which.Maximum.Should().Be(3);
        (await ActiveUsers(context, tenantId)).Should().Be(3);
    }

    [Fact]
    public async Task DirectPersistenceInsert_CannotBypassTheUserLimit()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxUsers: 3);
        context.Users.AddRange(User(tenantId, "a"), User(tenantId, "b"), User(tenantId, "c"));
        await context.SaveChangesAsync();

        context.Users.Add(User(tenantId, "d"));
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<PlanLimitExceededException>();
        (await ActiveUsers(context, tenantId)).Should().Be(3);
    }

    [Fact]
    public async Task PendingInvitation_ReservesASeat()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxUsers: 3);
        context.Users.AddRange(User(tenantId, "a"), User(tenantId, "b"));
        context.TeamInvitations.Add(Invite(tenantId, "pending@example.com"));
        await context.SaveChangesAsync();

        context.Users.Add(User(tenantId, "c"));
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<PlanLimitExceededException>();
    }

    [Fact]
    public async Task ExpiredInvitation_DoesNotReserveASeat()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxUsers: 1);
        context.TeamInvitations.Add(Invite(tenantId, "old@example.com", DateTime.UtcNow.AddMinutes(-5)));
        context.Users.Add(User(tenantId, "only"));
        await context.SaveChangesAsync();

        (await ActiveUsers(context, tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task RevokedInvitation_ReleasesTheSeat()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxUsers: 1);
        var invitation = Invite(tenantId, "seat@example.com");
        context.TeamInvitations.Add(invitation);
        await context.SaveChangesAsync();

        invitation.Status = InvitationStatus.Cancelled;
        context.Users.Add(User(tenantId, "replacement"));
        await context.SaveChangesAsync();

        (await ActiveUsers(context, tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task DeactivatedUser_DoesNotOccupyASeat()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxUsers: 1);
        var owner = User(tenantId, "owner");
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        owner.IsActive = false;
        context.Users.Add(User(tenantId, "replacement"));
        await context.SaveChangesAsync();

        (await ActiveUsers(context, tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task AcceptingInvitation_ConvertsTheReservedSeatWithoutNeedingAnother()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxUsers: 1);
        var invitation = Invite(tenantId, "join@example.com");
        context.TeamInvitations.Add(invitation);
        await context.SaveChangesAsync();

        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;
        context.Users.Add(User(tenantId, "join"));
        await context.SaveChangesAsync();

        (await ActiveUsers(context, tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentUserCreates_CannotExceedTheAllowance()
    {
        var database = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var options = Options(database);
        await using (var setup = Context(options, tenantId))
        {
            await SeedPlan(setup, tenantId, maxUsers: 1);
        }

        async Task<bool> CreateAsync(string name)
        {
            await using var context = Context(options, tenantId);
            context.Users.Add(User(tenantId, name));
            try
            {
                await context.SaveChangesAsync();
                return true;
            }
            catch (PlanLimitExceededException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(CreateAsync("one"), CreateAsync("two"));
        results.Count(succeeded => succeeded).Should().Be(1);

        await using var verify = Context(options, tenantId);
        (await ActiveUsers(verify, tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task Tasks_StopAtThePlanLimit()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxProjects: 5, maxTasks: 1);
        var project = new Project { TenantId = tenantId, Name = "Delivery" };
        context.Projects.Add(project);
        context.WorkTasks.Add(new WorkTask { TenantId = tenantId, ProjectId = project.Id, Title = "First" });
        await context.SaveChangesAsync();

        context.WorkTasks.Add(new WorkTask { TenantId = tenantId, ProjectId = project.Id, Title = "Second" });
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<PlanLimitExceededException>();
    }

    [Theory]
    [InlineData("customers")]
    [InlineData("products")]
    [InlineData("projects")]
    public async Task CountedResources_StopAtThePlanLimit(string resource)
    {
        var (context, tenantId, _, _) = await SeedAsync(
            maxCustomers: 1,
            maxProducts: 1,
            maxProjects: 1,
            maxTasks: 1);

        await AddResource(context, tenantId, resource, 1);
        var act = () => AddResource(context, tenantId, resource, 2);

        await act.Should().ThrowAsync<PlanLimitExceededException>();
    }

    [Fact]
    public async Task Storage_RejectsAFileThatExceedsTheRemainingBytes()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxStorageMb: 1);
        context.GeneratedReports.Add(Report(tenantId, new byte[1024 * 1024]));
        await context.SaveChangesAsync();

        context.GeneratedReports.Add(Report(tenantId, [1]));
        var act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<PlanLimitExceededException>();
    }

    [Fact]
    public async Task DeletedFile_ReleasesStorage()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxStorageMb: 1);
        var report = Report(tenantId, new byte[1024 * 1024]);
        context.GeneratedReports.Add(report);
        await context.SaveChangesAsync();

        report.IsDeleted = true;
        context.GeneratedReports.Add(Report(tenantId, new byte[1024 * 1024]));
        await context.SaveChangesAsync();

        var stored = await context.GeneratedReports.IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted);
        stored.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentUploads_CannotExceedStorage()
    {
        var database = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var options = Options(database);
        await using (var setup = Context(options, tenantId))
        {
            await SeedPlan(setup, tenantId, maxStorageMb: 1);
        }

        async Task<bool> UploadAsync()
        {
            await using var context = Context(options, tenantId);
            context.GeneratedReports.Add(Report(tenantId, new byte[600 * 1024]));
            try
            {
                await context.SaveChangesAsync();
                return true;
            }
            catch (PlanLimitExceededException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(UploadAsync(), UploadAsync());
        results.Count(succeeded => succeeded).Should().Be(1);
    }

    [Fact]
    public async Task AiRequests_AllowTheLastOne_AndRejectTheNext()
    {
        var (context, tenantId, provider, factory) = await SeedAsync(maxAiRequests: 2);
        var service = new TenantLimitService(context, provider, factory);

        await service.TryConsumeAiRequestAsync("first", inputTokens: 4, outputTokens: 6, estimatedProviderCostUsd: 0.01m);
        await service.TryConsumeAiRequestAsync("second");
        var over = () => service.TryConsumeAiRequestAsync("third");

        await over.Should().ThrowAsync<PlanLimitExceededException>();
        var usage = await context.TenantUsages.IgnoreQueryFilters().SingleAsync(x => x.TenantId == tenantId);
        usage.AiRequestsUsed.Should().Be(2);
        usage.AiInputTokens.Should().Be(4);
        usage.AiOutputTokens.Should().Be(6);
        usage.EstimatedProviderCostUsd.Should().Be(0.01m);
    }

    [Fact]
    public async Task AiRequest_RetryUsesTheSameOperationKeyOnce()
    {
        var (context, tenantId, provider, factory) = await SeedAsync(maxAiRequests: 5);
        var service = new TenantLimitService(context, provider, factory);

        await service.TryConsumeAiRequestAsync("logical-1");
        await service.TryConsumeAiRequestAsync("logical-1");

        var usage = await context.TenantUsages.IgnoreQueryFilters().SingleAsync(x => x.TenantId == tenantId);
        usage.AiRequestsUsed.Should().Be(1);
        (await context.AiUsageLedgers.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId)).Should().Be(1);
    }

    [Fact]
    public async Task RejectedAiRequest_DoesNotConsumeQuota()
    {
        var (context, tenantId, provider, factory) = await SeedAsync(maxAiRequests: 1);
        var service = new TenantLimitService(context, provider, factory);
        await service.TryConsumeAiRequestAsync("only");

        var rejected = () => service.TryConsumeAiRequestAsync("another");
        await rejected.Should().ThrowAsync<PlanLimitExceededException>();

        var usage = await context.TenantUsages.IgnoreQueryFilters().SingleAsync(x => x.TenantId == tenantId);
        usage.AiRequestsUsed.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentAiRequests_CannotOverspend()
    {
        var database = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var options = Options(database);
        await using (var setup = Context(options, tenantId))
        {
            await SeedPlan(setup, tenantId, maxAiRequests: 1);
        }

        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);
        var factory = new NamedFactory(options, provider);

        async Task<bool> ConsumeAsync(string key)
        {
            await using var context = Context(options, tenantId);
            var service = new TenantLimitService(context, provider, factory);
            try
            {
                await service.TryConsumeAiRequestAsync(key);
                return true;
            }
            catch (PlanLimitExceededException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(ConsumeAsync("a"), ConsumeAsync("b"));
        results.Count(succeeded => succeeded).Should().Be(1);
    }

    [Fact]
    public async Task MonthlyAiUsage_ResetsAtThePeriodBoundary_AndKeepsHistory()
    {
        var (context, tenantId, provider, factory) = await SeedAsync(maxAiRequests: 1);
        var service = new TenantLimitService(context, provider, factory);
        await service.TryConsumeAiRequestAsync("old-period");

        var usage = await context.TenantUsages.IgnoreQueryFilters().SingleAsync(x => x.TenantId == tenantId);
        usage.AiPeriodStart = DateTime.UtcNow.AddMonths(-2);
        await context.SaveChangesAsync();

        await service.TryConsumeAiRequestAsync("new-period");

        usage = await context.TenantUsages.IgnoreQueryFilters().SingleAsync(x => x.TenantId == tenantId);
        usage.AiRequestsUsed.Should().Be(1);
        (await context.AiUsageLedgers.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId)).Should().Be(2);
    }

    [Fact]
    public async Task Upgrade_RestoresCapacity_AndDowngrade_KeepsRowsButBlocksCreates()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxUsers: 1);
        context.Users.Add(User(tenantId, "kept"));
        await context.SaveChangesAsync();

        var plan = await context.SubscriptionPlans.SingleAsync();
        plan.MaxUsers = 2;
        await context.SaveChangesAsync();

        context.Users.Add(User(tenantId, "added-after-upgrade"));
        await context.SaveChangesAsync();

        plan.MaxUsers = 1;
        await context.SaveChangesAsync();
        (await ActiveUsers(context, tenantId)).Should().Be(2);

        context.Users.Add(User(tenantId, "blocked"));
        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<PlanLimitExceededException>();
        (await ActiveUsers(context, tenantId)).Should().Be(2);
    }

    [Fact]
    public async Task ExistingRecord_CanBeUpdatedWhileTheTenantIsOverLimit()
    {
        var (context, tenantId, _, _) = await SeedAsync(maxCustomers: 1);
        var customer = Customer(tenantId, "one@example.com");
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        var plan = await context.SubscriptionPlans.SingleAsync();
        plan.MaxCustomers = 0;
        await context.SaveChangesAsync();

        customer.FirstName = "Updated";
        await context.SaveChangesAsync();

        context.Customers.Add(Customer(tenantId, "two@example.com"));
        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<PlanLimitExceededException>();
        customer.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task TenantA_CannotConsumeOrReadTenantBUsage()
    {
        var database = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var options = Options(database);

        await using var contextA = Context(options, tenantA);
        await using var contextB = Context(options, tenantB);
        await SeedPlan(contextA, tenantA, maxProducts: 1);
        await SeedPlan(contextB, tenantB, maxProducts: 1);

        contextA.Products.Add(Product(tenantA, "A"));
        await contextA.SaveChangesAsync();
        contextB.Products.Add(Product(tenantB, "B"));
        await contextB.SaveChangesAsync();

        contextA.Products.Add(Product(tenantA, "A2"));
        var act = () => contextA.SaveChangesAsync();
        await act.Should().ThrowAsync<PlanLimitExceededException>();

        (await contextB.Products.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantB && !x.IsDeleted)).Should().Be(1);
        (await contextA.Products.CountAsync()).Should().Be(1);
        (await contextA.Products.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantB)).Should().BeTrue();
        (await contextA.Products.AnyAsync(x => x.TenantId == tenantB)).Should().BeFalse();
    }

    [Fact]
    public async Task MissingPlan_DoesNotBlockExistingUnscopedTestData()
    {
        var tenantId = Guid.NewGuid();
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);
        await using var context = Context(Options(Guid.NewGuid().ToString()), tenantId);
        context.Users.Add(User(tenantId, "unplanned"));
        await context.SaveChangesAsync();

        (await ActiveUsers(context, tenantId)).Should().Be(1);
    }

    private static async Task AddResource(BusinessOSDbContext context, Guid tenantId, string resource, int n)
    {
        switch (resource)
        {
            case "customers":
                context.Customers.Add(Customer(tenantId, $"c{n}@example.com"));
                break;
            case "products":
                context.Products.Add(Product(tenantId, $"P{n}"));
                break;
            case "projects":
                context.Projects.Add(new Project { TenantId = tenantId, Name = $"Project {n}" });
                break;
        }

        await context.SaveChangesAsync();
    }

    private static async Task<(BusinessOSDbContext Context, Guid TenantId, TenantProvider Provider, IDbContextFactory<BusinessOSDbContext> Factory)> SeedAsync(
        int maxUsers = 3,
        int maxCustomers = 500,
        int maxProducts = 1000,
        int maxProjects = 25,
        int maxTasks = 2000,
        int maxStorageMb = 5120,
        int maxAiRequests = 100)
    {
        var tenantId = Guid.NewGuid();
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);
        var options = Options(Guid.NewGuid().ToString());
        var context = Context(options, tenantId);
        await SeedPlan(context, tenantId, maxUsers, maxCustomers, maxProducts, maxProjects, maxTasks, maxStorageMb, maxAiRequests);
        return (context, tenantId, provider, new NamedFactory(options, provider));
    }

    private static async Task SeedPlan(
        BusinessOSDbContext context,
        Guid tenantId,
        int maxUsers = 3,
        int maxCustomers = 500,
        int maxProducts = 1000,
        int maxProjects = 25,
        int maxTasks = 2000,
        int maxStorageMb = 5120,
        int maxAiRequests = 100)
    {
        var plan = new SubscriptionPlan
        {
            Name = "Starter",
            Slug = "starter-" + tenantId.ToString("N")[..8],
            MonthlyPrice = 19,
            AnnualPrice = 182,
            MaxUsers = maxUsers,
            MaxCustomers = maxCustomers,
            MaxProducts = maxProducts,
            MaxProjects = maxProjects,
            MaxTasks = maxTasks,
            MaxStorageMb = maxStorageMb,
            MaxAiRequests = maxAiRequests,
            BaseCurrency = "USD",
            IsActive = true,
            IsPublic = true
        };
        context.SubscriptionPlans.Add(plan);
        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Quota Co",
            Slug = "quota-" + tenantId.ToString("N")[..8],
            Email = "quota@example.com",
            Phone = "1",
            Address = "1",
            BusinessType = "Retail",
            SubscriptionPlanId = plan.Id,
            SubscriptionPlan = plan.Name,
            OwnerUserId = "owner"
        });
        await context.SaveChangesAsync();
    }

    private static DbContextOptions<BusinessOSDbContext> Options(string database) =>
        new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(database)
            .Options;

    private static BusinessOSDbContext Context(DbContextOptions<BusinessOSDbContext> options, Guid tenantId)
    {
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);
        return new BusinessOSDbContext(options, provider);
    }

    private static ApplicationUser User(Guid tenantId, string name) => new()
    {
        Id = Guid.NewGuid().ToString(),
        TenantId = tenantId,
        UserName = name,
        NormalizedUserName = name.ToUpperInvariant(),
        Email = name + "@example.com",
        NormalizedEmail = (name + "@example.com").ToUpperInvariant(),
        FirstName = name,
        LastName = "User",
        IsActive = true,
        EmailConfirmed = true,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    private static TeamInvitation Invite(Guid tenantId, string email, DateTime? expiresAt = null) => new()
    {
        TenantId = tenantId,
        Email = email,
        RoleId = Guid.NewGuid(),
        Token = Guid.NewGuid().ToString("N"),
        Status = InvitationStatus.Pending,
        InvitedByUserId = "owner",
        ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(3)
    };

    private static Customer Customer(Guid tenantId, string email) => new()
    {
        TenantId = tenantId,
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = email,
        PhoneNumber = "1",
        Address = "1",
        City = "Lahore",
        Country = "PK",
        PostalCode = "54000"
    };

    private static Product Product(Guid tenantId, string sku) => new()
    {
        TenantId = tenantId,
        Name = sku,
        SKU = sku + Guid.NewGuid().ToString("N")[..6],
        CostPrice = 1,
        SalePrice = 2
    };

    private static GeneratedReport Report(Guid tenantId, byte[] content) => new()
    {
        TenantId = tenantId,
        ReportName = "Report",
        ReportType = "pdf",
        FileName = Guid.NewGuid().ToString("N") + ".pdf",
        GeneratedByUserId = "owner",
        GeneratedByName = "Owner",
        FileContent = content
    };

    private static Task<int> ActiveUsers(BusinessOSDbContext context, Guid tenantId) =>
        context.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive);

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

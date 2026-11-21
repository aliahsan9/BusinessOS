using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Growth;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BusinessOS.UnitTests.Growth;

public class GrowthServiceTests
{
    [Fact]
    public async Task SubmitLead_persists_contact_and_blocks_rapid_repeats()
    {
        var (sut, db, _) = Open();

        var created = await sut.SubmitLeadAsync(new MarketingLeadWrite
        {
            FullName = "Ada Lovelace",
            Email = "ada@example.com",
            Company = "Analytical Engines",
            Kind = "Demo",
            Topic = "Product demo",
            Subject = "Book a walkthrough",
            Message = "We want to see agency onboarding.",
            MarketingOptIn = true,
            SourcePath = "/demo"
        });

        created.Kind.Should().Be(MarketingLeadKinds.Demo);
        created.Status.Should().Be(MarketingLeadStatuses.New);
        db.MarketingLeads.Should().HaveCount(1);

        await sut.SubmitLeadAsync(Lead("ada@example.com", "Second"));
        await sut.SubmitLeadAsync(Lead("ada@example.com", "Third"));
        var act = () => sut.SubmitLeadAsync(Lead("ada@example.com", "Fourth"));
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Referral_code_is_tenant_scoped_and_validates()
    {
        var (sut, _, tenantId) = Open();
        var code = await sut.GetOrCreateReferralCodeAsync();
        code.Code.Should().NotBeNullOrWhiteSpace();
        code.SharePath.Should().Contain(code.Code);

        var again = await sut.GetOrCreateReferralCodeAsync();
        again.Code.Should().Be(code.Code);

        var valid = await sut.ValidateReferralCodeAsync(code.Code);
        valid.Valid.Should().BeTrue();

        var invalid = await sut.ValidateReferralCodeAsync("MISSING1");
        invalid.Valid.Should().BeFalse();

        await sut.AttributeReferralAsync(code.Code, "friend@example.com", tenantId);
        var after = await sut.GetOrCreateReferralCodeAsync();
        after.RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task Platform_analytics_marks_measured_estimated_and_unavailable()
    {
        var database = Guid.NewGuid().ToString();
        var (sut, db, tenantId) = Open(database, admin: true);
        db.SubscriptionPlans.AddRange(SubscriptionCatalog.CreatePlans());
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = tenantId,
            SubscriptionPlanId = SubscriptionCatalog.StarterPlanId,
            Status = SubscriptionStatus.Active,
            BillingInterval = BillingInterval.Monthly,
            StartDate = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20)
        });
        await db.SaveChangesAsync();

        var analytics = await sut.GetSaaSAnalyticsAsync();
        analytics.ActiveSubscriptions.Should().Be(1);
        analytics.MonthlyRecurringRevenue.Should().BeGreaterThan(0);
        analytics.AverageRevenuePerTenant.Should().NotBeNull();
        analytics.CustomerAcquisitionCost.Should().BeNull();
        analytics.CustomerLifetimeValue.Should().BeNull();
        analytics.MeasuredMetrics.Should().Contain("MonthlyRecurringRevenue");
        analytics.EstimatedMetrics.Should().Contain("EstimatedGrossMarginUsd");
        analytics.UnavailableMetrics.Should().Contain("CustomerAcquisitionCost");

        var outsider = Open(database, admin: false).Sut;
        var act = () => outsider.GetSaaSAnalyticsAsync();
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Support_ticket_can_be_created_anonymously_and_listed_by_admin()
    {
        var database = Guid.NewGuid().ToString();
        var (customer, _, _) = Open(database, admin: false, authenticated: false);
        var ticket = await customer.CreateSupportTicketAsync(new PlatformSupportTicketWrite
        {
            RequesterName = "Sam",
            RequesterEmail = "sam@example.com",
            Category = "Billing",
            Subject = "Invoice question",
            Message = "Where is my receipt?"
        });
        ticket.Status.Should().Be(PlatformSupportStatuses.Open);

        var (admin, _, _) = Open(database, admin: true);
        var rows = await admin.ListPlatformSupportTicketsAsync(null);
        rows.Should().Contain(item => item.Id == ticket.Id);
    }

    private static MarketingLeadWrite Lead(string email, string subject) => new()
    {
        FullName = "Ada Lovelace",
        Email = email,
        Topic = "Product demo",
        Subject = subject,
        Message = "Hello"
    };

    private static (GrowthService Sut, BusinessOSDbContext Db, Guid TenantId) Open(
        string? database = null,
        bool admin = false,
        bool authenticated = true)
    {
        var name = database ?? Guid.NewGuid().ToString();
        var (db, tenantId, provider) = TenantDatabase.Open(name: name);
        var factory = new NamedFactory(name, provider);
        var user = new Mock<ICurrentUserService>();
        if (authenticated)
        {
            user.SetupGet(item => item.UserId).Returns("user-1");
            user.SetupGet(item => item.Email).Returns("owner@example.com");
            user.SetupGet(item => item.TenantId).Returns(tenantId);
        }

        user.SetupGet(item => item.Roles).Returns(new List<string> { "Owner" });
        user.SetupGet(item => item.Permissions).Returns(new List<string>());
        var super = new Mock<ISuperAdminContext>();
        super.SetupGet(item => item.IsSuperAdmin).Returns(admin);
        super.SetupGet(item => item.CanAccessAllTenants).Returns(admin);
        return (new GrowthService(factory, user.Object, provider, super.Object), db, tenantId);
    }

    private sealed class NamedFactory : IDbContextFactory<BusinessOSDbContext>
    {
        private readonly string _name;
        private readonly TenantProvider _provider;

        public NamedFactory(string name, TenantProvider provider)
        {
            _name = name;
            _provider = provider;
        }

        public BusinessOSDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
                .UseInMemoryDatabase(_name)
                .Options;
            return new BusinessOSDbContext(options, _provider);
        }

        public Task<BusinessOSDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}

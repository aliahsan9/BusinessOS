using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Launch;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.Team.DTOs;
using BusinessOS.Application.Features.Team.Services;
using BusinessOS.Domain.Companies;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BusinessOS.UnitTests.Launch;

public class AgencyLaunchTests
{
    [Fact]
    public void Agency_templates_configure_the_shared_product()
    {
        var keys = AgencyTemplateCatalog.All.Select(template => template.Key).ToList();
        keys.Should().OnlyHaveUniqueItems();
        keys.Should().Equal("web-development", "software-consultancy", "mobile-app", "ai-automation", "digital-marketing");

        foreach (var template in AgencyTemplateCatalog.All)
        {
            template.Agents.Should().Contain(AgencyTemplateCatalog.LaunchAgents);
            template.Workflows.Should().Contain(AgencyTemplateCatalog.LaunchWorkflows);
            template.Modules.Should().NotBeEmpty();
            template.Widgets.Should().NotBeEmpty();
            foreach (var workflow in template.Workflows)
                NalvaWorkflowCatalog.TryGet(workflow, out _).Should().BeTrue();
            foreach (var agent in template.Agents)
                NalvaAgentCatalog.TryGet(agent, out _).Should().BeTrue();
            foreach (var role in template.Roles)
                RoleNames.All.Contains(role).Should().BeTrue();
        }

        BusinessTypeCatalog.Defaults.Select(item => item.Key).Should().Contain(keys);
    }

    [Fact]
    public async Task Agency_setup_configures_modules_agents_workflows_and_queues_work()
    {
        var (db, sut, team, _) = Open();
        db.RbacRoles.Add(new Role { Name = "Employee", IsActive = true });
        await db.SaveChangesAsync();

        await sut.SaveCompanyAsync(Company());
        await sut.SaveProfileAsync(new AgencyProfileWrite
        {
            TemplateKey = "web-development",
            Services = "Websites",
            Technologies = "Angular",
            TeamSize = "8",
            TargetClients = "Local businesses",
            Goals = "Ship client work faster"
        });
        await sut.SaveWorkspaceAsync(new AgencyWorkspaceWrite
        {
            Emails = ["dev@example.com"],
            InitialProjectName = "First client site"
        });
        await sut.SaveWorkforceAsync(new AgencyWorkforceWrite
        {
            ApprovalPreference = "RequireApproval",
            MonthlyBudgetUsd = 25
        });

        var planBefore = db.Tenants.Single().SubscriptionPlan;
        await sut.SavePlanAsync(new AgencyPlanWrite { PlanKey = "starter" });
        db.Tenants.Single().SubscriptionPlan.Should().Be(planBefore);
        db.BillingTransactions.Should().BeEmpty();

        var setup = await sut.SaveObjectiveAsync(new AgencyObjectiveWrite
        {
            Objective = "Analyze our agency operations and suggest the top five improvements."
        });

        setup.Workspace.IsCompleted.Should().BeTrue();
        setup.Workspace.FirstTaskQueued.Should().BeTrue();
        setup.Workspace.SelectedPlanKey.Should().Be("starter");
        var task = db.NalvaTasks.Single();
        task.Status.Should().Be(NalvaStatuses.Queued);
        task.AgentKey.Should().Be("ceo");
        db.NalvaCompanyAgents.Count(agent => agent.Status == NalvaStatuses.Active).Should().Be(5);
        db.NalvaWorkflowDefinitions.Count(item => item.IsActive).Should().BeGreaterThan(0);
        db.NalvaWorkflowDefinitions.Select(item => item.Key).Should().Contain("lead-qualification");
        db.Projects.Should().ContainSingle(project => project.Name == "First client site");
        db.UserOnboardingProgress.Single().IsCompleted.Should().BeTrue();
        db.NalvaCompanySettings.Single().MonthlyBudgetUsd.Should().Be(25);
        db.Tenants.Single().EnabledModulesJson.Should().Contain("projects");
        team.Verify(item => item.InviteMemberAsync(
            It.Is<InviteTeamMemberRequest>(request => request.Email == "dev@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);

        var act = () => sut.SaveWorkforceAsync(new AgencyWorkforceWrite { ApprovalPreference = "Automatic" });
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Early_access_invitation_is_single_use()
    {
        var database = Guid.NewGuid().ToString();
        var (db, sut, _, companyId) = Open(database, admin: true);
        var created = await sut.CreateInvitationAsync(new CreateInvitationWrite
        {
            Email = "owner@agency.example",
            AgencyName = "North Agency",
            ValidDays = 14
        });
        db.PlatformAuditLogs.Should().ContainSingle(item => item.Action == "launch.invitation.created");
        db.PlatformAuditLogs.Single().MetadataJson.Should().NotContain(created.Code);

        var redeemed = await sut.RedeemInvitationAsync(created.Code);
        redeemed.AgencyName.Should().Be("North Agency");
        var again = () => sut.RedeemInvitationAsync(created.Code);
        await again.Should().ThrowAsync<BadRequestException>();

        var (_, other, _, _) = Open(database);
        var reused = () => other.RedeemInvitationAsync(created.Code);
        await reused.Should().ThrowAsync<BadRequestException>();

        created.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        var expired = db.EarlyAccessInvitations.Single(item => item.Id == created.Id);
        expired.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        expired.Status = "Pending";
        expired.RedeemedCompanyId = null;
        await db.SaveChangesAsync();
        var late = () => sut.RedeemInvitationAsync(created.Code);
        await late.Should().ThrowAsync<BadRequestException>();
        db.EarlyAccessInvitations.Single().Status.Should().Be("Expired");

        await sut.SetInvitationRequiredAsync(true);
        var blocked = () => sut.SaveCompanyAsync(Company());
        await blocked.Should().ThrowAsync<BadRequestException>();
        companyId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Feedback_sets_backlog_priority_and_usage_rejects_unknown_events()
    {
        var (db, sut, _, _) = Open();
        var bug = await sut.SubmitFeedbackAsync(new LaunchFeedbackWrite
        {
            Kind = "bug",
            Message = "Saving a customer returns an error."
        });
        var security = await sut.SubmitFeedbackAsync(new LaunchFeedbackWrite
        {
            Kind = "bug",
            Message = "A security cross-tenant read is possible."
        });
        var feature = await sut.SubmitFeedbackAsync(new LaunchFeedbackWrite
        {
            Kind = "feature",
            Message = "Add a client portal theme."
        });

        bug.Priority.Should().Be(3);
        security.Priority.Should().Be(1);
        feature.Priority.Should().Be(6);
        db.ProductBacklogItems.Select(item => item.Priority).Should().BeEquivalentTo([3, 1, 6]);
        db.ProductUsageEvents.Should().NotContain(item => item.EventKey.Contains("customer", StringComparison.OrdinalIgnoreCase));

        var unknown = () => sut.RecordUsageAsync("customer.email");
        await unknown.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Export_stays_inside_the_company_and_deletion_keeps_rows()
    {
        var database = Guid.NewGuid().ToString();
        var (leftDb, left, _, leftId) = Open(database);
        leftDb.Customers.Add(new Customer
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@left.example",
            PhoneNumber = "1",
            Address = "1 Road",
            City = "Lahore",
            Country = "Pakistan",
            PostalCode = "54000"
        });
        await leftDb.SaveChangesAsync();

        var (rightDb, right, _, _) = Open(database, role: "Owner");
        var package = await right.ExportAsync();
        package.SummaryJson.Should().NotContain("ada@left.example");
        package.SummaryJson.Should().Contain("passwords");

        leftDb.Customers.Should().ContainSingle();
        var requested = await left.RequestDeletionAsync();
        requested.Status.Should().Be("Requested");
        var (adminDb, admin, _, _) = Open(database, admin: true);
        var confirmed = await admin.ConfirmDeletionAsync(leftId);
        confirmed.Status.Should().Be("Deactivated");

        var stored = await adminDb.Customers.IgnoreQueryFilters().SingleAsync(item => item.Email == "ada@left.example");
        stored.IsDeleted.Should().BeFalse();
        (await adminDb.Tenants.IgnoreQueryFilters().SingleAsync(item => item.Id == leftId)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Platform_analytics_counts_companies_without_customer_records()
    {
        var database = Guid.NewGuid().ToString();
        var (db, _, _, _) = Open(database);
        db.NalvaExecutions.Add(new NalvaExecution
        {
            AgentKey = "ceo",
            Objective = "Hidden objective",
            Status = NalvaStatuses.Failed,
            Model = "gpt-4o-mini",
            PricingVersion = "test"
        });
        db.AuthSessions.Add(new AuthSession
        {
            UserId = "user-1",
            TenantId = db.Tenants.Single().Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            LastUsedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var (_, admin, _, _) = Open(database, admin: true);
        var analytics = await admin.GetPlatformAnalyticsAsync();
        analytics.Companies.Should().BeGreaterThan(0);
        analytics.FailedExecutions.Should().Be(1);
        analytics.WeeklyActiveUsers.Should().Be(1);
        analytics.AgentUsage.Should().Contain(item => item.Name == "ceo" && item.Count == 1);
        analytics.AgentUsage.Select(item => item.Name).Should().NotContain("Hidden objective");

        var outsider = new Mock<ISuperAdminContext>();
        outsider.SetupGet(item => item.IsSuperAdmin).Returns(false);
        var denied = new AgencyLaunchService(db, User(db.Tenants.Single().Id), outsider.Object, new Mock<ITeamService>().Object);
        var act = () => denied.GetPlatformAnalyticsAsync();
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    private static AgencyCompanyWrite Company() => new()
    {
        Name = "North Agency",
        Country = "Pakistan",
        Currency = "PKR",
        Timezone = "UTC",
        Website = "https://north.example",
        Description = "Web studio"
    };

    private static (BusinessOSDbContext Db, AgencyLaunchService Sut, Mock<ITeamService> Team, Guid TenantId) Open(
        string? database = null,
        bool admin = false,
        string role = "Owner")
    {
        var (db, tenantId, _) = TenantDatabase.Open(name: database);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "North Agency",
            Slug = "north-" + tenantId.ToString("N")[..8],
            BusinessType = "custom",
            Email = "owner@example.com",
            Phone = "1",
            Address = "1 Main",
            Country = "Pakistan",
            OwnerUserId = "user-1",
            SubscriptionPlan = "Free",
            SubscriptionPlanId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Status = TenantStatus.Active,
            IsActive = true,
            Timezone = "UTC",
            Currency = "USD"
        });
        db.SaveChanges();

        var team = new Mock<ITeamService>();
        team.Setup(item => item.InviteMemberAsync(It.IsAny<InviteTeamMemberRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamInvitationDto());
        var super = new Mock<ISuperAdminContext>();
        super.SetupGet(item => item.IsSuperAdmin).Returns(admin);
        super.SetupGet(item => item.CanAccessAllTenants).Returns(admin);
        return (db, new AgencyLaunchService(db, User(tenantId, role), super.Object, team.Object), team, tenantId);
    }

    private static ICurrentUserService User(Guid tenantId, string role = "Owner")
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.UserId).Returns("user-1");
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        user.SetupGet(item => item.Email).Returns("owner@example.com");
        user.SetupGet(item => item.Roles).Returns(new List<string> { role });
        user.SetupGet(item => item.Permissions).Returns(new List<string>());
        user.Setup(item => item.HasPermission(It.IsAny<string>())).Returns(false);
        return user.Object;
    }
}

using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Enterprise;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Enterprise;
using BusinessOS.Infrastructure.Identity;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.UnitTests.Enterprise;

public sealed class Phase10EnterpriseTests
{
    [Fact]
    public async Task OrgScope_BlocksCrossBranchAccess_ForScopedUsers()
    {
        await using var harness = await EnterpriseHarness.CreateAsync();
        var branchA = await harness.Org.SaveBranchAsync(null, new CompanyBranchWriteRequest
        {
            Name = "HQ",
            IsHeadquarters = true,
            Code = "HQ"
        });
        var branchB = await harness.Org.SaveBranchAsync(null, new CompanyBranchWriteRequest
        {
            Name = "West",
            Code = "W1"
        });

        harness.SetCurrentUser("mgr-1", ["Manager"], [PermissionCodes.OrgBranchView]);
        harness.Db.Employees.Add(new Employee
        {
            TenantId = harness.TenantId,
            FirstName = "Pat",
            LastName = "Manager",
            Email = "pat@example.com",
            Phone = "1",
            Designation = "Manager",
            Salary = 1,
            JoiningDate = DateTime.UtcNow.Date,
            UserId = "mgr-1",
            PrimaryBranchId = branchA.Id
        });
        await harness.Db.SaveChangesAsync();

        var scope = await harness.Scope.GetCurrentScopeAsync();
        scope.IsTenantWide.Should().BeFalse();
        scope.BranchIds.Should().Contain(branchA.Id);
        scope.BranchIds.Should().NotContain(branchB.Id);

        var act = async () => await harness.Scope.EnsureCanAccessBranchAsync(branchB.Id);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task TemporaryPermission_CanBeGrantedAndRevoked()
    {
        await using var harness = await EnterpriseHarness.CreateAsync();
        harness.SetCurrentUser("owner-1", ["Owner"], PermissionCodes.BuildOwnerPermissions().ToArray());

        var user = new ApplicationUser
        {
            Id = "user-temp",
            UserName = "temp@example.com",
            Email = "temp@example.com",
            TenantId = harness.TenantId,
            FirstName = "Temp",
            LastName = "User",
            IsActive = true
        };
        (await harness.Users.CreateAsync(user)).Succeeded.Should().BeTrue();

        var grant = await harness.Rbac.GrantTemporaryAsync(new TemporaryPermissionWriteRequest
        {
            UserId = user.Id,
            PermissionCode = PermissionCodes.FinanceView,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            Reason = "month-end"
        });

        (await harness.Rbac.ResolveEffectivePermissionsAsync(user.Id))
            .Should().Contain(PermissionCodes.FinanceView);

        await harness.Rbac.RevokeTemporaryAsync(grant.Id);
        (await harness.Rbac.ResolveEffectivePermissionsAsync(user.Id))
            .Should().NotContain(PermissionCodes.FinanceView);
    }

    [Fact]
    public async Task ApprovalPolicy_BlocksAgentSelfApproval()
    {
        await using var harness = await EnterpriseHarness.CreateAsync();
        harness.SetCurrentUser("owner-1", ["Owner"], PermissionCodes.BuildOwnerPermissions().ToArray());

        await harness.Approvals.SavePolicyAsync(null, new ApprovalPolicyWriteRequest
        {
            Name = "Expense over 500",
            TriggerType = ApprovalTriggerTypes.ExpenseAmount,
            AmountThreshold = 500,
            Mode = "Sequential",
            DeadlineHours = 24,
            Steps = [new ApprovalPolicyStepDto(1, "Role", "Manager", null, null, false)]
        });

        var request = await harness.Approvals.EvaluateAndEnqueueAsync(new ApprovalEvaluateRequest
        {
            TriggerType = ApprovalTriggerTypes.ExpenseAmount,
            SubjectType = "Expense",
            SubjectId = Guid.NewGuid().ToString(),
            Title = "Laptop",
            Amount = 900
        });
        request.Should().NotBeNull();

        harness.SetCurrentUser("agent-1", ["Agent"], [PermissionCodes.AgentExecute]);
        var deny = async () => await harness.Approvals.DecideAsync(request!.Id, new ApprovalDecisionWriteRequest { Approve = true });
        await deny.Should().ThrowAsync<ForbiddenException>();

        var pending = await harness.Db.ApprovalRequests.AsNoTracking().SingleAsync(x => x.Id == request!.Id);
        pending.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task AdvancedSecurity_IsRequired_ForEnterpriseApis()
    {
        await using var harness = await EnterpriseHarness.CreateAsync(enterprise: false);
        harness.SetCurrentUser("owner-1", ["Owner"], PermissionCodes.BuildOwnerPermissions().ToArray());
        var act = async () => await harness.Org.GetBranchesAsync();
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AgentGovernance_DeniesConfiguredTools()
    {
        await using var harness = await EnterpriseHarness.CreateAsync();
        harness.SetCurrentUser("owner-1", ["Owner"], PermissionCodes.BuildOwnerPermissions().ToArray());
        harness.Db.NalvaCompanyAgents.Add(new NalvaCompanyAgent
        {
            TenantId = harness.TenantId,
            AgentKey = "finance",
            DisplayName = "Finance",
            Description = "Finance",
            RoleTitle = "CFO",
            Category = "Finance",
            DeniedToolsJson = """["CreateRefund"]"""
        });
        await harness.Db.SaveChangesAsync();

        var act = async () => await harness.Agents.EnsureToolAllowedAsync("finance", "CreateRefund", null);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AuditExport_WritesExportAuditEvent()
    {
        await using var harness = await EnterpriseHarness.CreateAsync();
        harness.SetCurrentUser("owner-1", ["Owner"], PermissionCodes.BuildOwnerPermissions().ToArray());
        await harness.Audit.LogAsync(new EnterpriseAuditWriteRequest
        {
            Category = EnterpriseAuditCategories.Administrative,
            Action = "TestEvent",
            ActorUserId = "owner-1",
            ActorType = "User"
        });

        var bytes = await harness.Audit.ExportCsvAsync(new EnterpriseAuditQuery());
        bytes.Length.Should().BeGreaterThan(0);
        var events = await harness.Db.EnterpriseAuditEvents.AsNoTracking().ToListAsync();
        events.Should().Contain(x => x.Action == "AuditExport");
        events.Should().Contain(x => x.Action == "TestEvent");
    }
}

file sealed class EnterpriseHarness : IAsyncDisposable
{
    private readonly ServiceProvider _sp;
    public Guid TenantId { get; }
    public BusinessOSDbContext Db { get; }
    public IEnterpriseOrganizationService Org { get; }
    public IOrgScopeService Scope { get; }
    public IEnterpriseRbacService Rbac { get; }
    public IApprovalGovernanceService Approvals { get; }
    public IEnterpriseAgentGovernanceService Agents { get; }
    public IEnterpriseAuditService Audit { get; }
    public UserManager<ApplicationUser> Users { get; }
    private readonly TestCurrentUser _currentUser;

    private EnterpriseHarness(ServiceProvider sp, Guid tenantId, BusinessOSDbContext db, TestCurrentUser currentUser)
    {
        _sp = sp;
        TenantId = tenantId;
        Db = db;
        _currentUser = currentUser;
        Org = sp.GetRequiredService<IEnterpriseOrganizationService>();
        Scope = sp.GetRequiredService<IOrgScopeService>();
        Rbac = sp.GetRequiredService<IEnterpriseRbacService>();
        Approvals = sp.GetRequiredService<IApprovalGovernanceService>();
        Agents = sp.GetRequiredService<IEnterpriseAgentGovernanceService>();
        Audit = sp.GetRequiredService<IEnterpriseAuditService>();
        Users = sp.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public static async Task<EnterpriseHarness> CreateAsync(bool enterprise = true)
    {
        var tenantId = Guid.NewGuid();
        var tenantProvider = new TenantProvider();
        tenantProvider.SetTenantId(tenantId);
        var currentUser = new TestCurrentUser(tenantId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<ITenantProvider>(tenantProvider);
        services.AddSingleton<ICurrentUserService>(currentUser);
        services.AddDbContext<BusinessOSDbContext>(o => o.UseInMemoryDatabase("phase10-" + Guid.NewGuid()));
        services.AddDbContextFactory<BusinessOSDbContext>(o => o.UseInMemoryDatabase("phase10-factory-" + Guid.NewGuid()));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<BusinessOSDbContext>();
        services.AddScoped<IAuthorizationVersionService, AuthorizationVersionService>();
        services.AddScoped<IEnterpriseEntitlementGuard, EnterpriseEntitlementGuard>();
        services.AddScoped<IOrgScopeService, OrgScopeService>();
        services.AddScoped<IEnterpriseOrganizationService, EnterpriseOrganizationService>();
        services.AddScoped<IEnterpriseRbacService, EnterpriseRbacService>();
        services.AddScoped<IApprovalGovernanceService, ApprovalGovernanceService>();
        services.AddScoped<IEnterpriseAuditService, EnterpriseAuditService>();
        services.AddScoped<IEnterpriseAgentGovernanceService, EnterpriseAgentGovernanceService>();

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<BusinessOSDbContext>();

        var plan = new SubscriptionPlan
        {
            Id = enterprise ? SubscriptionCatalog.EnterprisePlanId : SubscriptionCatalog.StarterPlanId,
            Name = enterprise ? "Enterprise" : "Starter",
            Slug = enterprise ? "enterprise" : "starter",
            HasMultiBranch = enterprise,
            HasAdvancedReports = enterprise,
            HasAiAssistant = true,
            IsActive = true
        };
        db.SubscriptionPlans.Add(plan);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            Slug = "acme-" + tenantId.ToString("N")[..8],
            Email = "owner@acme.test",
            Phone = "0",
            Address = "n/a",
            BusinessType = "General",
            OwnerUserId = "owner-1",
            SubscriptionPlanId = plan.Id,
            SubscriptionPlan = plan.Slug,
            Plan = plan
        });
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = tenantId,
            SubscriptionPlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        return new EnterpriseHarness(sp, tenantId, db, currentUser);
    }

    public void SetCurrentUser(string userId, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        _currentUser.UserId = userId;
        _currentUser.Roles = roles.ToList();
        _currentUser.Permissions = permissions.ToList();
    }

    public ValueTask DisposeAsync()
    {
        _sp.Dispose();
        return ValueTask.CompletedTask;
    }
}

file sealed class TestCurrentUser : ICurrentUserService
{
    public TestCurrentUser(Guid tenantId) => TenantId = tenantId;
    public string? UserId { get; set; }
    public string? Email => null;
    public Guid? TenantId { get; }
    public IReadOnlyList<string> Roles { get; set; } = [];
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public bool HasPermission(string permissionCode) =>
        Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
}

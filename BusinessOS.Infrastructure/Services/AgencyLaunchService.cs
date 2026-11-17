using System.Security.Cryptography;
using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Experience;
using BusinessOS.Application.Features.Launch;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.Team.DTOs;
using BusinessOS.Application.Features.Team.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Companies;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class AgencyLaunchService : IAgencyLaunchService
{
    private const string FirstObjectiveKey = "agency-launch-first-objective";
    private const string ApprovalPreference = "RequireApproval";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> UsageEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "onboarding.company",
        "onboarding.profile",
        "onboarding.workspace",
        "onboarding.workforce",
        "onboarding.plan",
        "onboarding.completed",
        "feature.opened",
        "feedback.submitted",
        "support.requested",
        "data.export",
        "data.delete-requested"
    };

    private static readonly HashSet<string> FeedbackKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "feedback", "feature", "bug", "survey", "support"
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ISuperAdminContext _admin;
    private readonly ITeamService _team;

    public AgencyLaunchService(
        IApplicationDbContext db,
        ICurrentUserService user,
        ISuperAdminContext admin,
        ITeamService team)
    {
        _db = db;
        _user = user;
        _admin = admin;
        _team = team;
    }

    public LaunchNoticeDto GetNotice() => Notice();

    public async Task<AgencySetupDto> GetSetupAsync(CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        return await SetupAsync(cancellationToken);
    }

    public async Task<AgencySetupDto> SaveCompanyAsync(AgencyCompanyWrite request, CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        await EnsureInvitedAsync(cancellationToken);
        var name = Required(request.Name, "Agency name", 200);
        var country = Required(request.Country, "Country", 100);
        var currency = Required(request.Currency, "Currency", 8).ToUpperInvariant();
        if (currency.Length < 3 || currency.Any(character => !char.IsLetter(character)))
            throw new BadRequestException("Currency must be a 3 to 8 letter code.");
        var timezone = Required(request.Timezone, "Timezone", 64);
        var website = Optional(request.Website, 500);
        if (website is not null &&
            !website.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !website.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Website must start with http:// or https://.");
        }

        var tenant = await CompanyAsync(cancellationToken);
        tenant.Name = name;
        tenant.Country = country;
        tenant.Currency = currency;
        tenant.Timezone = timezone;
        tenant.Website = website;
        tenant.Description = Optional(request.Description, 2000);
        tenant.LogoUrl = Optional(request.LogoUrl, 500);
        tenant.UpdatedAt = DateTime.UtcNow;

        var settings = await _db.TenantSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new TenantSettings { TenantId = tenant.Id };
            _db.TenantSettings.Add(settings);
        }

        settings.Currency = currency;
        settings.Timezone = timezone;
        settings.LogoUrl = tenant.LogoUrl;
        settings.UpdatedAt = DateTime.UtcNow;

        var workspace = await WorkspaceAsync(cancellationToken);
        workspace.CompanySaved = true;
        workspace.CurrentStep = NextStep(workspace);
        AddUsage("onboarding.company");
        await _db.SaveChangesAsync(cancellationToken);
        return await SetupAsync(cancellationToken);
    }

    public async Task<AgencySetupDto> SaveProfileAsync(AgencyProfileWrite request, CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        await EnsureInvitedAsync(cancellationToken);
        var workspace = await RequiredWorkspaceAsync(cancellationToken);
        if (!workspace.CompanySaved)
            throw new BadRequestException("Save the company information first.");
        if (!AgencyTemplateCatalog.TryGet(request.TemplateKey, out var template) || template is null)
            throw new BadRequestException("Choose one of the agency templates.");

        var tenant = await CompanyAsync(cancellationToken);
        tenant.BusinessType = template.Key;
        tenant.EnabledModulesJson = template.ModulesJson;
        tenant.UpdatedAt = DateTime.UtcNow;

        workspace.TemplateKey = template.Key;
        workspace.Services = Optional(request.Services, 2000);
        workspace.Technologies = Optional(request.Technologies, 2000);
        workspace.TeamSize = Optional(request.TeamSize, 64);
        workspace.TargetClients = Optional(request.TargetClients, 2000);
        workspace.Goals = Optional(request.Goals, 4000);
        workspace.ModulesJson = template.ModulesJson;
        workspace.RolesJson = JsonSerializer.Serialize(template.Roles);
        workspace.ProfileSaved = true;
        workspace.CurrentStep = NextStep(workspace);

        await ApplyWidgetsAsync(template, cancellationToken);
        await ActivateWorkflowsAsync(template, tenant.Timezone, cancellationToken);
        AddUsage("onboarding.profile");
        await _db.SaveChangesAsync(cancellationToken);
        return await SetupAsync(cancellationToken);
    }

    public async Task<AgencySetupDto> SaveWorkspaceAsync(AgencyWorkspaceWrite request, CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        await EnsureInvitedAsync(cancellationToken);
        var workspace = await RequiredWorkspaceAsync(cancellationToken);
        if (!workspace.ProfileSaved || string.IsNullOrWhiteSpace(workspace.TemplateKey))
            throw new BadRequestException("Choose an agency template first.");
        if (!AgencyTemplateCatalog.TryGet(workspace.TemplateKey, out var template) || template is null)
            throw new BadRequestException("Choose one of the agency templates.");

        var modules = CleanList(request.Modules, 12, 32);
        if (modules.Count == 0)
            modules = template.Modules.ToList();
        if (modules.Any(module => !AgencyTemplateCatalog.KnownModules.Contains(module, StringComparer.OrdinalIgnoreCase)))
            throw new BadRequestException("One of the selected modules is not part of CompanyOS.");

        var roles = CleanList(request.Roles, 12, 64);
        if (roles.Count == 0)
            roles = template.Roles.ToList();
        if (roles.Any(role => !RoleNames.All.Contains(role)))
            throw new BadRequestException("One of the selected roles is not a CompanyOS role.");

        var emails = CleanEmails(request.Emails);
        var projectName = Optional(request.InitialProjectName, 200);
        var tenant = await CompanyAsync(cancellationToken);
        tenant.EnabledModulesJson = JsonSerializer.Serialize(modules);
        tenant.UpdatedAt = DateTime.UtcNow;

        workspace.ModulesJson = tenant.EnabledModulesJson;
        workspace.RolesJson = JsonSerializer.Serialize(roles);
        workspace.InvitedEmailsJson = JsonSerializer.Serialize(emails);
        workspace.InitialProjectName = projectName;
        if (!string.IsNullOrWhiteSpace(projectName))
            await EnsureProjectAsync(workspace, projectName, cancellationToken);

        var (created, note) = await InviteAsync(emails, roles, cancellationToken);
        workspace.InvitesCreated = created;
        workspace.InviteNote = note;
        workspace.WorkspaceSaved = true;
        workspace.CurrentStep = NextStep(workspace);
        AddUsage("onboarding.workspace");
        await _db.SaveChangesAsync(cancellationToken);
        return await SetupAsync(cancellationToken);
    }

    public async Task<AgencySetupDto> SaveWorkforceAsync(AgencyWorkforceWrite request, CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        await EnsureInvitedAsync(cancellationToken);
        var workspace = await RequiredWorkspaceAsync(cancellationToken);
        if (!workspace.WorkspaceSaved || !AgencyTemplateCatalog.TryGet(workspace.TemplateKey, out var template) || template is null)
            throw new BadRequestException("Finish the workspace step first.");

        var preference = request.ApprovalPreference?.Trim() ?? "";
        if (!preference.Equals(ApprovalPreference, StringComparison.OrdinalIgnoreCase) &&
            !preference.Equals("require-approval", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Agency setup keeps approval on. Sensitive actions are not switched to automatic execution.");
        }

        if (request.MonthlyBudgetUsd is < 0 or > 5000)
            throw new BadRequestException("The monthly AI budget must be between 0 and 5000 USD.");

        var agents = CleanList(request.AgentKeys, 20, 80);
        if (agents.Count == 0)
            agents = template.Agents.ToList();
        if (agents.Any(agent => !template.Agents.Contains(agent, StringComparer.OrdinalIgnoreCase)))
            throw new BadRequestException("Activate agents from the selected agency template.");
        if (agents.Any(agent => !NalvaAgentCatalog.TryGet(agent, out _)))
            throw new BadRequestException("One of the selected agents is not in the CompanyOS workforce.");

        var previous = ModuleList.Parse(workspace.AgentsJson);
        foreach (var agent in agents)
            ActivateAgent(agent);
        foreach (var removed in previous.Where(agent => !agents.Contains(agent, StringComparer.OrdinalIgnoreCase)))
            await DeactivateAgentAsync(removed, cancellationToken);

        workspace.AgentsJson = JsonSerializer.Serialize(agents);
        workspace.ApprovalPreference = ApprovalPreference;
        if (request.MonthlyBudgetUsd is > 0)
        {
            var budget = decimal.Round(request.MonthlyBudgetUsd.Value, 2);
            workspace.MonthlyAiBudgetUsd = budget;
            var settings = await _db.NalvaCompanySettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                settings = new NalvaCompanySettings();
                _db.NalvaCompanySettings.Add(settings);
            }

            settings.MonthlyBudgetUsd = budget;
            settings.CostAlert = true;
        }

        workspace.WorkforceSaved = true;
        workspace.CurrentStep = NextStep(workspace);
        AddUsage("onboarding.workforce");
        await _db.SaveChangesAsync(cancellationToken);
        return await SetupAsync(cancellationToken);
    }

    public async Task<AgencySetupDto> SavePlanAsync(AgencyPlanWrite request, CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        await EnsureInvitedAsync(cancellationToken);
        var workspace = await RequiredWorkspaceAsync(cancellationToken);
        if (!workspace.WorkforceSaved)
            throw new BadRequestException("Finish the AI workforce step first.");

        var plan = SubscriptionCatalog.CreatePlans()
            .FirstOrDefault(item => item.Slug.Equals(request.PlanKey?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (plan is null)
            throw new BadRequestException("Choose a published CompanyOS plan.");

        workspace.SelectedPlanKey = plan.Slug;
        workspace.PlanSaved = true;
        workspace.CurrentStep = NextStep(workspace);
        AddUsage("onboarding.plan");
        await _db.SaveChangesAsync(cancellationToken);
        return await SetupAsync(cancellationToken);
    }

    public async Task<AgencySetupDto> SaveObjectiveAsync(AgencyObjectiveWrite request, CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        await EnsureInvitedAsync(cancellationToken);
        var workspace = await RequiredWorkspaceAsync(cancellationToken);
        if (!workspace.PlanSaved)
            throw new BadRequestException("Choose a plan first. Checkout still happens on the billing page.");

        var objective = Required(request.Objective, "First objective", 2000);
        if (objective.Length < 8)
            throw new BadRequestException("Describe the first objective in a short paragraph.");

        var ceo = await _db.NalvaCompanyAgents.FirstOrDefaultAsync(
            agent => agent.AgentKey == "ceo" && agent.Status == NalvaStatuses.Active,
            cancellationToken);
        if (ceo is null)
            throw new BadRequestException("Activate the AI CEO before assigning the first objective.");

        var task = await _db.NalvaTasks.FirstOrDefaultAsync(item => item.IdempotencyKey == FirstObjectiveKey, cancellationToken);
        if (task is null)
        {
            task = new NalvaTask
            {
                AgentKey = "ceo",
                Objective = objective,
                Status = NalvaStatuses.Queued,
                Priority = 2,
                RunAfter = DateTime.UtcNow,
                IdempotencyKey = FirstObjectiveKey,
                InitiatorUserId = _user.UserId
            };
            _db.NalvaTasks.Add(task);
        }
        else if (task.Status is NalvaStatuses.Queued or NalvaStatuses.Paused or NalvaStatuses.Pending)
        {
            task.Objective = objective;
            task.Status = NalvaStatuses.Queued;
        }

        workspace.FirstObjective = objective;
        workspace.FirstTaskId = task.Id;
        workspace.CompletedAt = DateTime.UtcNow;
        workspace.CurrentStep = 6;
        await CompleteLegacyOnboardingAsync(cancellationToken);
        AddUsage("onboarding.completed");
        await _db.SaveChangesAsync(cancellationToken);
        return await SetupAsync(cancellationToken);
    }

    public async Task<InvitationRedeemDto> RedeemInvitationAsync(string code, CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        if (normalized.Length is < 6 or > 32)
            throw new BadRequestException("That invitation is not valid.");

        var invitation = await _db.EarlyAccessInvitations
            .FirstOrDefaultAsync(item => item.Code == normalized && !item.IsDeleted, cancellationToken);
        if (invitation is null || invitation.Status != "Pending")
            throw new BadRequestException("That invitation is not valid.");
        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = "Expired";
            await _db.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("That invitation is not valid.");
        }

        var companyId = CompanyId();
        var already = await _db.EarlyAccessInvitations.AnyAsync(
            item => item.RedeemedCompanyId == companyId && item.Status == "Redeemed" && !item.IsDeleted,
            cancellationToken);
        if (already)
            throw new BadRequestException("This company has already redeemed an invitation.");

        invitation.Status = "Redeemed";
        invitation.RedeemedCompanyId = companyId;
        invitation.RedeemedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return new InvitationRedeemDto(true, invitation.AgencyName);
    }

    public async Task<LaunchFeedbackDto> SubmitFeedbackAsync(LaunchFeedbackWrite request, CancellationToken cancellationToken = default)
    {
        RequireUser();
        var kind = (request.Kind ?? "").Trim().ToLowerInvariant();
        if (!FeedbackKinds.Contains(kind))
            throw new BadRequestException("Choose feedback, a feature request, a bug, a survey, or support.");
        var message = Required(request.Message, "Message", 4000);
        if (request.Rating is < 1 or > 5)
            throw new BadRequestException("A rating must be from 1 to 5.");
        if (kind == "survey" && request.Rating is null)
            throw new BadRequestException("A satisfaction survey needs a rating from 1 to 5.");

        var subject = Optional(request.Subject, 200) ?? DefaultSubject(kind);
        var priority = PriorityFor(kind, message, request.Rating);
        var feedback = new LaunchFeedback
        {
            UserId = _user.UserId!,
            Kind = kind,
            Subject = subject,
            Message = message,
            Rating = request.Rating,
            Page = Optional(request.Page, 200),
            Status = "Open",
            Priority = priority
        };
        var backlog = new ProductBacklogItem
        {
            SourceCompanyId = CompanyId(),
            FeedbackId = feedback.Id,
            Title = subject,
            Kind = kind,
            Priority = priority,
            Status = "Open"
        };
        feedback.BacklogItemId = backlog.Id;
        _db.LaunchFeedback.Add(feedback);
        _db.ProductBacklogItems.Add(backlog);
        AddUsage(kind == "support" ? "support.requested" : "feedback.submitted");
        await _db.SaveChangesAsync(cancellationToken);
        return MapFeedback(feedback);
    }

    public async Task<IReadOnlyList<LaunchFeedbackDto>> ListFeedbackAsync(CancellationToken cancellationToken = default)
    {
        RequireUser();
        var rows = await _db.LaunchFeedback.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return rows.Select(MapFeedback).ToList();
    }

    public async Task RecordUsageAsync(string eventKey, CancellationToken cancellationToken = default)
    {
        RequireUser();
        var key = (eventKey ?? "").Trim().ToLowerInvariant();
        if (!UsageEvents.Contains(key))
            throw new BadRequestException("That usage event is not collected.");
        AddUsage(key);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CompanyAnalyticsDto> GetCompanyAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        RequireUser();
        var since = DateTime.UtcNow.AddDays(-7);
        var events = await _db.ProductUsageEvents.AsNoTracking()
            .Where(item => item.OccurredAt >= since)
            .GroupBy(item => item.EventKey)
            .Select(group => new NamedCountDto(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var agents = await _db.NalvaExecutions.AsNoTracking()
            .GroupBy(item => item.AgentKey)
            .Select(group => new NamedCountDto(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var workspace = await _db.AgencyWorkspaces.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return new CompanyAnalyticsDto(
            workspace?.CompletedAt is not null,
            events.Sum(item => item.Count),
            events,
            await _db.LaunchFeedback.CountAsync(item => item.Status == "Open", cancellationToken),
            await _db.LaunchFeedback.CountAsync(item => item.Kind == "support", cancellationToken),
            await _db.NalvaExecutions.CountAsync(cancellationToken),
            await _db.NalvaExecutions.CountAsync(item => item.Status == NalvaStatuses.Failed, cancellationToken),
            await _db.NalvaWorkflowRuns.CountAsync(cancellationToken),
            agents.OrderByDescending(item => item.Count).Take(12).ToList());
    }

    public async Task<CompanyDataRequestDto> ExportAsync(CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        var tenant = await CompanyAsync(cancellationToken);
        var workspace = await _db.AgencyWorkspaces.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var customers = await _db.Customers.AsNoTracking()
            .OrderBy(item => item.LastName)
            .Take(101)
            .Select(item => new { item.FirstName, item.LastName, item.Email })
            .ToListAsync(cancellationToken);
        var leads = await _db.Leads.AsNoTracking()
            .OrderBy(item => item.Name)
            .Take(101)
            .Select(item => new { item.Name, item.Status, item.Source })
            .ToListAsync(cancellationToken);
        var projects = await _db.Projects.AsNoTracking()
            .OrderBy(item => item.Name)
            .Take(101)
            .Select(item => new { item.Name, item.Status })
            .ToListAsync(cancellationToken);
        var invoices = await _db.Invoices.AsNoTracking()
            .OrderByDescending(item => item.InvoiceDate)
            .Take(101)
            .Select(item => new { item.InvoiceNumber, item.Status, item.GrandTotal, item.OutstandingAmount })
            .ToListAsync(cancellationToken);

        var package = new
        {
            exportedAt = DateTime.UtcNow,
            company = new
            {
                tenant.Name,
                tenant.Country,
                tenant.Currency,
                tenant.Timezone,
                tenant.BusinessType,
                modules = ModuleList.Parse(tenant.EnabledModulesJson)
            },
            agency = workspace is null ? null : new
            {
                workspace.TemplateKey,
                workspace.Services,
                workspace.Technologies,
                workspace.TeamSize,
                workspace.TargetClients,
                workspace.Goals
            },
            counts = new
            {
                customers = await _db.Customers.CountAsync(cancellationToken),
                leads = await _db.Leads.CountAsync(cancellationToken),
                projects = await _db.Projects.CountAsync(cancellationToken),
                invoices = await _db.Invoices.CountAsync(cancellationToken),
                storedFiles = await _db.StoredFiles.CountAsync(cancellationToken),
                knowledgeDocuments = await _db.AiDocuments.CountAsync(cancellationToken)
            },
            customers = customers.Take(100),
            leads = leads.Take(100),
            projects = projects.Take(100),
            invoices = invoices.Take(100),
            truncated = customers.Count > 100 || leads.Count > 100 || projects.Count > 100 || invoices.Count > 100,
            omitted = new[]
            {
                "passwords",
                "refresh tokens",
                "integration credentials",
                "payment methods",
                "file contents",
                "knowledge document text",
                "free-text notes"
            }
        };

        var dataRequest = new CompanyDataRequest
        {
            RequestedByUserId = _user.UserId!,
            Kind = "export",
            Status = "Ready",
            SummaryJson = JsonSerializer.Serialize(package, JsonOptions),
            CompletedAt = DateTime.UtcNow
        };
        _db.CompanyDataRequests.Add(dataRequest);
        AddUsage("data.export");
        await _db.SaveChangesAsync(cancellationToken);
        return MapDataRequest(dataRequest);
    }

    public async Task<CompanyDataRequestDto?> LatestExportAsync(CancellationToken cancellationToken = default)
    {
        RequireCompanyAdmin();
        var row = await _db.CompanyDataRequests.AsNoTracking()
            .Where(item => item.Kind == "export")
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : MapDataRequest(row);
    }

    public async Task<CompanyDataRequestDto> RequestDeletionAsync(CancellationToken cancellationToken = default)
    {
        RequireOwner();
        var existing = await _db.CompanyDataRequests
            .FirstOrDefaultAsync(item => item.Kind == "delete" && item.Status == "Requested", cancellationToken);
        if (existing is not null)
            return MapDataRequest(existing);

        var dataRequest = new CompanyDataRequest
        {
            RequestedByUserId = _user.UserId!,
            Kind = "delete",
            Status = "Requested"
        };
        _db.CompanyDataRequests.Add(dataRequest);
        AddUsage("data.delete-requested");
        await _db.SaveChangesAsync(cancellationToken);
        return MapDataRequest(dataRequest);
    }

    public async Task<EarlyAccessInvitationDto> CreateInvitationAsync(CreateInvitationWrite request, CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        var email = Required(request.Email, "Email", 256).ToLowerInvariant();
        if (!email.Contains('@') || email.Contains(' '))
            throw new BadRequestException("Enter the agency contact email.");
        var agency = Required(request.AgencyName, "Agency name", 200);
        var days = Math.Clamp(request.ValidDays ?? 30, 1, 90);
        var code = await NewCodeAsync(cancellationToken);
        var invitation = new EarlyAccessInvitation
        {
            Code = code,
            Email = email,
            AgencyName = agency,
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(days),
            CreatedByUserId = _user.UserId ?? "platform"
        };
        _db.EarlyAccessInvitations.Add(invitation);
        AddAudit("launch.invitation.created", "EarlyAccessInvitation", invitation.Id.ToString(), null, $"{{\"agency\":\"{Escape(agency)}\"}}");
        await _db.SaveChangesAsync(cancellationToken);
        return MapInvitation(invitation);
    }

    public async Task<IReadOnlyList<EarlyAccessInvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        var rows = await _db.EarlyAccessInvitations.AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(MapInvitation).ToList();
    }

    public async Task RevokeInvitationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        var invitation = await _db.EarlyAccessInvitations.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Invitation was not found.");
        if (invitation.Status == "Redeemed")
            throw new BadRequestException("A redeemed invitation cannot be revoked.");
        invitation.Status = "Revoked";
        AddAudit("launch.invitation.revoked", "EarlyAccessInvitation", invitation.Id.ToString(), invitation.RedeemedCompanyId, null);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetInvitationRequiredAsync(bool required, CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(item => item.Key == LaunchSettings.RequireInvitation, cancellationToken);
        if (setting is null)
        {
            setting = new PlatformSetting { Key = LaunchSettings.RequireInvitation };
            _db.PlatformSettings.Add(setting);
        }

        setting.Value = required ? "true" : "false";
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedByUserId = _user.UserId;
        AddAudit("launch.invitation.required", "PlatformSetting", setting.Key, null, required ? "{\"required\":true}" : "{\"required\":false}");
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlatformAnalyticsDto> GetPlatformAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        var since = DateTime.UtcNow.AddDays(-7);
        var companies = await _db.Tenants.IgnoreQueryFilters().CountAsync(item => !item.IsDeleted, cancellationToken);
        var active = await _db.Tenants.IgnoreQueryFilters().CountAsync(
            item => !item.IsDeleted && item.IsActive && (item.Status == TenantStatus.Active || item.Status == TenantStatus.Trial),
            cancellationToken);
        var agencyCompleted = await _db.AgencyWorkspaces.IgnoreQueryFilters()
            .CountAsync(item => !item.IsDeleted && item.CompletedAt != null, cancellationToken);
        var legacyCompleted = await _db.UserOnboardingProgress.CountAsync(item => item.IsCompleted && !item.IsDeleted, cancellationToken);
        var usageCompanies = await _db.ProductUsageEvents.IgnoreQueryFilters()
            .Where(item => !item.IsDeleted && item.OccurredAt >= since)
            .Select(item => item.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var sessionCompanies = await _db.AuthSessions.AsNoTracking()
            .Where(item => item.LastUsedAt >= since && item.RevokedAt == null)
            .Select(item => item.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var weeklyUsers = await _db.AuthSessions.AsNoTracking()
            .Where(item => item.LastUsedAt >= since && item.RevokedAt == null)
            .Select(item => item.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        var usage = await _db.ProductUsageEvents.IgnoreQueryFilters()
            .Where(item => !item.IsDeleted)
            .GroupBy(item => item.EventKey)
            .Select(group => new NamedCountDto(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var agents = await _db.NalvaExecutions.IgnoreQueryFilters()
            .Where(item => !item.IsDeleted)
            .GroupBy(item => item.AgentKey)
            .Select(group => new NamedCountDto(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        return new PlatformAnalyticsDto(
            companies,
            agencyCompleted,
            legacyCompleted,
            active,
            usageCompanies.Union(sessionCompanies).Count(),
            weeklyUsers,
            await _db.NalvaExecutions.IgnoreQueryFilters().CountAsync(item => !item.IsDeleted, cancellationToken),
            await _db.NalvaExecutions.IgnoreQueryFilters().CountAsync(item => !item.IsDeleted && item.Status == NalvaStatuses.Failed, cancellationToken),
            await _db.NalvaWorkflowRuns.IgnoreQueryFilters().CountAsync(item => !item.IsDeleted, cancellationToken),
            await _db.TenantSubscriptions.IgnoreQueryFilters().CountAsync(item => !item.IsDeleted && item.Status == SubscriptionStatus.Trial, cancellationToken),
            await _db.TenantSubscriptions.IgnoreQueryFilters().CountAsync(item => !item.IsDeleted && item.Status == SubscriptionStatus.Active, cancellationToken),
            await _db.TenantSubscriptions.IgnoreQueryFilters().CountAsync(item => !item.IsDeleted && item.Status == SubscriptionStatus.Cancelled, cancellationToken),
            await _db.LaunchFeedback.IgnoreQueryFilters().CountAsync(item => !item.IsDeleted && item.Kind == "support", cancellationToken),
            await _db.LaunchFeedback.IgnoreQueryFilters().CountAsync(item => !item.IsDeleted && item.Status == "Open", cancellationToken),
            usage,
            agents.OrderByDescending(item => item.Count).Take(12).ToList());
    }

    public async Task<IReadOnlyList<LaunchFeedbackDto>> ListPlatformFeedbackAsync(CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        var rows = await _db.LaunchFeedback.IgnoreQueryFilters().AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(MapFeedback).ToList();
    }

    public async Task<IReadOnlyList<BacklogItemDto>> ListBacklogAsync(CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        var rows = await _db.ProductBacklogItems.AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Priority)
            .ThenByDescending(item => item.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(MapBacklog).ToList();
    }

    public async Task<BacklogItemDto> UpdateBacklogAsync(Guid id, BacklogUpdate request, CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        if (request.Priority is < 1 or > 6)
            throw new BadRequestException("Priority must be from 1 (security) to 6 (new feature).");
        var status = request.Status?.Trim() ?? "";
        if (status is not ("Open" or "InProgress" or "Done" or "Deferred"))
            throw new BadRequestException("Backlog status must be Open, InProgress, Done, or Deferred.");

        var item = await _db.ProductBacklogItems.FirstOrDefaultAsync(row => row.Id == id && !row.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Backlog item was not found.");
        item.Priority = request.Priority;
        item.Status = status;
        item.UpdatedAt = DateTime.UtcNow;
        AddAudit("launch.backlog.updated", "ProductBacklogItem", item.Id.ToString(), item.SourceCompanyId, $"{{\"priority\":{item.Priority},\"status\":\"{status}\"}}");
        await _db.SaveChangesAsync(cancellationToken);
        return MapBacklog(item);
    }

    public async Task<CompanyDataRequestDto> ConfirmDeletionAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        RequirePlatform();
        var dataRequest = await _db.CompanyDataRequests.IgnoreQueryFilters()
            .Where(item => item.TenantId == companyId && !item.IsDeleted && item.Kind == "delete" && item.Status == "Requested")
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("There is no open deletion request for that company.");
        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == companyId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Company was not found.");

        tenant.Status = TenantStatus.Cancelled;
        tenant.IsActive = false;
        tenant.UpdatedAt = DateTime.UtcNow;
        dataRequest.Status = "Deactivated";
        dataRequest.CompletedAt = DateTime.UtcNow;
        AddAudit("launch.deletion.confirmed", "Tenant", tenant.Id.ToString(), tenant.Id, "{\"retained\":true}");
        await _db.SaveChangesAsync(cancellationToken);
        return MapDataRequest(dataRequest);
    }

    private async Task<AgencySetupDto> SetupAsync(CancellationToken cancellationToken)
    {
        var workspace = await _db.AgencyWorkspaces.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var companyId = CompanyId();
        return new AgencySetupDto(
            Notice(),
            await InvitationRequiredAsync(cancellationToken),
            await _db.EarlyAccessInvitations.AnyAsync(
                item => item.RedeemedCompanyId == companyId && item.Status == "Redeemed" && !item.IsDeleted,
                cancellationToken),
            workspace is null ? EmptyWorkspace() : await MapWorkspaceAsync(workspace, cancellationToken),
            AgencyTemplateCatalog.All.Select(MapTemplate).ToList(),
            Plans());
    }

    private async Task EnsureInvitedAsync(CancellationToken cancellationToken)
    {
        if (!await InvitationRequiredAsync(cancellationToken))
            return;

        var companyId = CompanyId();
        var redeemed = await _db.EarlyAccessInvitations.AnyAsync(
            item => item.RedeemedCompanyId == companyId && item.Status == "Redeemed" && !item.IsDeleted,
            cancellationToken);
        if (!redeemed)
            throw new BadRequestException("An early-access invitation is required before agency setup can continue.");
    }

    private async Task<bool> InvitationRequiredAsync(CancellationToken cancellationToken)
    {
        var value = await _db.PlatformSettings.AsNoTracking()
            .Where(item => item.Key == LaunchSettings.RequireInvitation)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Tenant> CompanyAsync(CancellationToken cancellationToken)
    {
        var companyId = CompanyId();
        return await _db.Tenants.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken)
            ?? throw new NotFoundException("Company was not found.");
    }

    private async Task<AgencyWorkspace> WorkspaceAsync(CancellationToken cancellationToken)
    {
        var workspace = await _db.AgencyWorkspaces.FirstOrDefaultAsync(cancellationToken);
        if (workspace is not null)
            return workspace;

        workspace = new AgencyWorkspace();
        _db.AgencyWorkspaces.Add(workspace);
        return workspace;
    }

    private async Task<AgencyWorkspace> RequiredWorkspaceAsync(CancellationToken cancellationToken)
    {
        return await _db.AgencyWorkspaces.FirstOrDefaultAsync(cancellationToken)
            ?? throw new BadRequestException("Save the company information first.");
    }

    private async Task ApplyWidgetsAsync(AgencyTemplateDefinition template, CancellationToken cancellationToken)
    {
        var visible = template.Widgets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var widgets = AgencyTemplateCatalog.KnownWidgets
            .Select(id => new DashboardWidgetDto(id, id, "md", visible.Contains(id)))
            .ToList();
        var userId = _user.UserId!;
        var preference = await _db.DashboardPreferences.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = new DashboardPreference { UserId = userId };
            _db.DashboardPreferences.Add(preference);
        }

        preference.WidgetsJson = JsonSerializer.Serialize(widgets, JsonOptions);
    }

    private async Task ActivateWorkflowsAsync(AgencyTemplateDefinition template, string timezone, CancellationToken cancellationToken)
    {
        var zone = ResolveZone(timezone);
        foreach (var key in template.Workflows)
        {
            if (!NalvaWorkflowCatalog.TryGet(key, out var workflow) || workflow is null)
                continue;

            var definition = await _db.NalvaWorkflowDefinitions.FirstOrDefaultAsync(item => item.Key == workflow.Key, cancellationToken);
            if (definition is null)
            {
                definition = new NalvaWorkflowDefinition { Key = workflow.Key };
                _db.NalvaWorkflowDefinitions.Add(definition);
            }

            definition.Name = workflow.Name;
            definition.Description = workflow.Description;
            definition.TriggerKind = workflow.TriggerKind;
            definition.TriggerEvent = workflow.TriggerEvent;
            definition.IntervalMinutes = workflow.IntervalMinutes;
            definition.TimeZoneId = zone;
            definition.IsActive = true;
            definition.MaxToolCalls = NalvaLimits.MaxToolCalls;
            definition.StepsJson = JsonSerializer.Serialize(workflow.Steps.Select(step => new NalvaWorkflowStepWrite
            {
                Key = step.Key,
                AgentKey = step.AgentKey,
                Objective = step.Objective,
                Tools = step.Tools.ToList(),
                Condition = step.Condition,
                ParallelGroup = step.ParallelGroup
            }), JsonOptions);
        }
    }

    private void ActivateAgent(string key)
    {
        var definition = NalvaAgentCatalog.Get(key);
        var row = _db.NalvaCompanyAgents.Local.FirstOrDefault(item => item.AgentKey == definition.Key)
            ?? _db.NalvaCompanyAgents.FirstOrDefault(item => item.AgentKey == definition.Key);
        if (row is null)
        {
            row = new NalvaCompanyAgent { AgentKey = definition.Key };
            _db.NalvaCompanyAgents.Add(row);
        }

        row.DisplayName = definition.Name;
        row.Description = definition.Description;
        row.RoleTitle = definition.Role;
        row.Category = definition.Category;
        row.Status = NalvaStatuses.Active;
        row.AutonomyLevel = definition.DefaultAutonomy;
        row.Model = definition.DefaultModel;
        row.DailyExecutionLimit = definition.DailyExecutionLimit;
        row.ToolsJson = JsonSerializer.Serialize(definition.Tools);
        row.PermissionsJson = JsonSerializer.Serialize(definition.Permissions);
        row.ResponsibilitiesJson = JsonSerializer.Serialize(definition.Responsibilities);
        row.ApprovalActionsJson = JsonSerializer.Serialize(definition.ApprovalActions);
        row.DefinitionVersion = definition.Version;
        row.IsCustom = false;
        row.ActivatedAt = DateTime.UtcNow;
        row.ActivatedByUserId = _user.UserId;
    }

    private async Task DeactivateAgentAsync(string key, CancellationToken cancellationToken)
    {
        var row = await _db.NalvaCompanyAgents.FirstOrDefaultAsync(item => item.AgentKey == key, cancellationToken);
        if (row is not null)
            row.Status = NalvaStatuses.Inactive;
    }

    private async Task EnsureProjectAsync(AgencyWorkspace workspace, string name, CancellationToken cancellationToken)
    {
        Project? project = null;
        if (workspace.InitialProjectId is Guid projectId)
            project = await _db.Projects.FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);

        if (project is null)
        {
            project = new Project
            {
                Name = name,
                Description = "Created during agency setup.",
                Status = ProjectStatus.Planning
            };
            _db.Projects.Add(project);
            workspace.InitialProjectId = project.Id;
            return;
        }

        project.Name = name;
        workspace.InitialProjectId = project.Id;
    }

    private async Task<(int Created, string? Note)> InviteAsync(
        IReadOnlyList<string> emails,
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken)
    {
        if (emails.Count == 0)
            return (0, null);

        var preferred = roles.Where(role => !role.Equals(RoleNames.Owner, StringComparison.OrdinalIgnoreCase)).ToList();
        if (preferred.Count == 0)
            preferred.Add(RoleNames.Employee);
        var installed = await _db.RbacRoles.Where(role => role.IsActive).ToListAsync(cancellationToken);
        var role = preferred
            .Select(name => installed.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(item => item is not null);
        if (role is null)
            return (0, "The selected role is not installed, so the addresses were stored and can be sent from Team.");

        var created = 0;
        string? note = null;
        foreach (var email in emails)
        {
            try
            {
                await _team.InviteMemberAsync(new InviteTeamMemberRequest(email, role.Id), cancellationToken);
                created++;
            }
            catch (AppException ex)
            {
                note = ex.Message;
            }
        }

        return (created, note);
    }

    private async Task CompleteLegacyOnboardingAsync(CancellationToken cancellationToken)
    {
        var userId = _user.UserId!;
        var progress = await _db.UserOnboardingProgress.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (progress is null)
        {
            progress = new UserOnboardingProgress { UserId = userId };
            _db.UserOnboardingProgress.Add(progress);
        }

        progress.CurrentStep = 8;
        progress.IsCompleted = true;
        progress.IsSkipped = false;
        progress.CompletedAt = DateTime.UtcNow;
        progress.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> NewCodeAsync(CancellationToken cancellationToken)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var chars = new char[8];
            for (var index = 0; index < chars.Length; index++)
                chars[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            var code = new string(chars);
            var exists = await _db.EarlyAccessInvitations.AnyAsync(item => item.Code == code, cancellationToken);
            if (!exists)
                return code;
        }

        throw new BadRequestException("An invitation code could not be created. Try again.");
    }

    private void AddUsage(string eventKey)
    {
        _db.ProductUsageEvents.Add(new ProductUsageEvent
        {
            EventKey = eventKey,
            OccurredAt = DateTime.UtcNow
        });
    }

    private void AddAudit(string action, string entityType, string entityId, Guid? companyId, string? metadata)
    {
        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorUserId = _user.UserId ?? "platform",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            SubjectTenantId = companyId,
            MetadataJson = metadata,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<AgencyWorkspaceDto> MapWorkspaceAsync(AgencyWorkspace workspace, CancellationToken cancellationToken)
    {
        var queued = false;
        if (workspace.FirstTaskId is Guid taskId)
        {
            queued = await _db.NalvaTasks.AsNoTracking().AnyAsync(
                item => item.Id == taskId && item.Status != NalvaStatuses.Cancelled,
                cancellationToken);
        }

        return new AgencyWorkspaceDto(
            workspace.CurrentStep,
            workspace.CompletedAt is not null,
            workspace.CompanySaved,
            workspace.ProfileSaved,
            workspace.WorkspaceSaved,
            workspace.WorkforceSaved,
            workspace.PlanSaved,
            workspace.TemplateKey,
            workspace.Services,
            workspace.Technologies,
            workspace.TeamSize,
            workspace.TargetClients,
            workspace.Goals,
            ModuleList.Parse(workspace.ModulesJson),
            ModuleList.Parse(workspace.InvitedEmailsJson),
            ModuleList.Parse(workspace.RolesJson),
            workspace.InitialProjectName,
            workspace.InitialProjectId,
            ModuleList.Parse(workspace.AgentsJson),
            workspace.ApprovalPreference,
            workspace.MonthlyAiBudgetUsd,
            workspace.SelectedPlanKey,
            workspace.FirstObjective,
            workspace.FirstTaskId,
            queued,
            workspace.InvitesCreated,
            workspace.InviteNote);
    }

    private static AgencyWorkspaceDto EmptyWorkspace() => new(
        1, false, false, false, false, false, false,
        null, null, null, null, null, null,
        [], [], [], null, null, [],
        ApprovalPreference, null, null, null, null, false, 0, null);

    private static AgencyTemplateDto MapTemplate(AgencyTemplateDefinition template) => new(
        template.Key,
        template.Name,
        template.Description,
        template.Modules,
        template.Modules.Select(AgencyTemplateCatalog.ModuleLabel).ToList(),
        template.Widgets,
        template.Roles,
        template.Workflows,
        template.Agents);

    private static IReadOnlyList<LaunchPlanDto> Plans() =>
        SubscriptionCatalog.CreatePlans().Select(plan => new LaunchPlanDto(
            plan.Slug,
            plan.Name,
            plan.MonthlyPrice,
            plan.AnnualPrice,
            plan.TrialDays,
            plan.MaxAiRequests,
            plan.MaxAgentExecutions,
            plan.MaxAutomationRuns,
            SubscriptionCatalog.BaseCurrency,
            "Choosing a plan here does not charge a card. Checkout, renewal, and cancellation stay on the billing page.")).ToList();

    private static LaunchNoticeDto Notice() => new(
        LaunchNoticeCatalog.Product,
        LaunchNoticeCatalog.Audience,
        LaunchNoticeCatalog.Status,
        LaunchNoticeCatalog.Available,
        LaunchNoticeCatalog.Limitations,
        LaunchNoticeCatalog.NotInThisRelease,
        LaunchNoticeCatalog.DataHandling,
        LaunchNoticeCatalog.SubscriptionTerms,
        LaunchNoticeCatalog.Support);

    private static LaunchFeedbackDto MapFeedback(LaunchFeedback item) => new(
        item.Id, item.Kind, item.Subject, item.Message, item.Rating, item.Page, item.Status, item.Priority, item.CreatedAt);

    private static EarlyAccessInvitationDto MapInvitation(EarlyAccessInvitation item) => new(
        item.Id, item.Code, item.Email, item.AgencyName, item.Status, item.ExpiresAt, item.RedeemedCompanyId, item.CreatedAt);

    private static BacklogItemDto MapBacklog(ProductBacklogItem item) => new(
        item.Id, item.SourceCompanyId, item.Title, item.Kind, item.Priority, item.Status, item.CreatedAt);

    private static CompanyDataRequestDto MapDataRequest(CompanyDataRequest item) => new(
        item.Id,
        item.Kind,
        item.Status,
        item.SummaryJson,
        item.CreatedAt,
        item.CompletedAt,
        item.Kind == "delete"
            ? "A platform administrator confirms this request. Confirmation deactivates the company and keeps the rows until an operational purge. Download the export before requesting deletion."
            : "This package is a bounded export of the company's own operational records. Passwords, tokens, integration secrets, payment methods, file contents, and note text are omitted.");

    private static int NextStep(AgencyWorkspace workspace)
    {
        if (workspace.CompletedAt is not null)
            return 6;
        if (!workspace.CompanySaved)
            return 1;
        if (!workspace.ProfileSaved)
            return 2;
        if (!workspace.WorkspaceSaved)
            return 3;
        if (!workspace.WorkforceSaved)
            return 4;
        if (!workspace.PlanSaved)
            return 5;
        return 6;
    }

    private static int PriorityFor(string kind, string message, int? rating)
    {
        var text = message.ToLowerInvariant();
        if (text.Contains("security", StringComparison.Ordinal) ||
            text.Contains("cross-tenant", StringComparison.Ordinal) ||
            text.Contains("data leak", StringComparison.Ordinal))
        {
            return 1;
        }

        if (text.Contains("corrupt", StringComparison.Ordinal) ||
            text.Contains("data loss", StringComparison.Ordinal) ||
            text.Contains("wrong company", StringComparison.Ordinal))
        {
            return 2;
        }

        return kind switch
        {
            "bug" => 3,
            "support" => 4,
            "feedback" => 4,
            "survey" => rating is <= 2 ? 4 : 5,
            "feature" => 6,
            _ => 6
        };
    }

    private static string DefaultSubject(string kind) => kind switch
    {
        "feature" => "Feature request",
        "bug" => "Bug report",
        "survey" => "Satisfaction survey",
        "support" => "Support request",
        _ => "Product feedback"
    };

    private static string ResolveZone(string timezone)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return timezone;
        }
        catch (TimeZoneNotFoundException)
        {
            return "UTC";
        }
        catch (InvalidTimeZoneException)
        {
            return "UTC";
        }
    }

    private static string? Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "'", StringComparison.Ordinal);

    private Guid CompanyId() =>
        _user.TenantId ?? throw new UnauthorizedException("Company context is required.");

    private void RequireUser()
    {
        if (string.IsNullOrWhiteSpace(_user.UserId) || _user.TenantId is null)
            throw new UnauthorizedException("User context is required.");
    }

    private void RequireCompanyAdmin()
    {
        RequireUser();
        if (_user.Roles.Any(role => role is RoleNames.Owner or RoleNames.Admin) ||
            _user.HasPermission(PermissionCodes.TenantManage))
        {
            return;
        }

        throw new ForbiddenException("Agency setup is limited to a company owner or administrator.");
    }

    private void RequireOwner()
    {
        RequireUser();
        if (_user.Roles.Contains(RoleNames.Owner))
            return;
        throw new ForbiddenException("Only the company owner can request deletion.");
    }

    private void RequirePlatform()
    {
        if (!_admin.IsSuperAdmin)
            throw new ForbiddenException("Platform administrator access is required.");
    }

    private static string Required(string? value, string label, int max)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0)
            throw new BadRequestException($"{label} is required.");
        if (text.Length > max)
            throw new BadRequestException($"{label} must be {max} characters or fewer.");
        return text;
    }

    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        if (text.Length > max)
            throw new BadRequestException($"A field must be {max} characters or fewer.");
        return text;
    }

    private static List<string> CleanList(IEnumerable<string>? values, int maxItems, int maxLength)
    {
        var list = new List<string>();
        foreach (var value in values ?? [])
        {
            var text = value?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength || list.Contains(text, StringComparer.OrdinalIgnoreCase))
                continue;
            list.Add(text);
            if (list.Count == maxItems)
                break;
        }

        return list;
    }

    private static List<string> CleanEmails(IEnumerable<string>? values)
    {
        var list = new List<string>();
        foreach (var value in values ?? [])
        {
            var email = value?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !email.Contains('@') || email.Contains(' '))
                continue;
            if (list.Contains(email, StringComparer.Ordinal))
                continue;
            list.Add(email);
            if (list.Count == 10)
                break;
        }

        return list;
    }
}

using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>One agency setup record per company. Templates configure this company; they do not create another application.</summary>
public class AgencyWorkspace : AuditableEntity
{
    public Guid TenantId { get; set; }
    public int CurrentStep { get; set; } = 1;
    public bool CompanySaved { get; set; }
    public bool ProfileSaved { get; set; }
    public bool WorkspaceSaved { get; set; }
    public bool WorkforceSaved { get; set; }
    public bool PlanSaved { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TemplateKey { get; set; }
    public string? Services { get; set; }
    public string? Technologies { get; set; }
    public string? TeamSize { get; set; }
    public string? TargetClients { get; set; }
    public string? Goals { get; set; }
    public string ModulesJson { get; set; } = "[]";
    public string InvitedEmailsJson { get; set; } = "[]";
    public string RolesJson { get; set; } = "[]";
    public string? InitialProjectName { get; set; }
    public Guid? InitialProjectId { get; set; }
    public string AgentsJson { get; set; } = "[]";
    public string ApprovalPreference { get; set; } = "RequireApproval";
    public decimal? MonthlyAiBudgetUsd { get; set; }
    public string? SelectedPlanKey { get; set; }
    public string? FirstObjective { get; set; }
    public Guid? FirstTaskId { get; set; }
    public int InvitesCreated { get; set; }
    public string? InviteNote { get; set; }
}

/// <summary>Platform invitation for the controlled early-access launch. Not tenant-owned until it is redeemed.</summary>
public class EarlyAccessInvitation : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string AgencyName { get; set; } = default!;
    public string Status { get; set; } = "Pending";
    public DateTime ExpiresAt { get; set; }
    public Guid? RedeemedCompanyId { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public string CreatedByUserId { get; set; } = default!;
}

public class LaunchFeedback : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;
    public string Kind { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Message { get; set; } = default!;
    public int? Rating { get; set; }
    public string? Page { get; set; }
    public string Status { get; set; } = "Open";
    public int Priority { get; set; }
    public Guid? BacklogItemId { get; set; }
}

/// <summary>Product usage name and time. Payloads with customer data are not stored.</summary>
public class ProductUsageEvent : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string EventKey { get; set; } = default!;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Platform backlog. SourceCompanyId is not a tenant filter, so one company cannot query another company's items through the tenant filter.</summary>
public class ProductBacklogItem : AuditableEntity
{
    public Guid? SourceCompanyId { get; set; }
    public Guid? FeedbackId { get; set; }
    public string Title { get; set; } = default!;
    public string Kind { get; set; } = default!;
    public int Priority { get; set; }
    public string Status { get; set; } = "Open";
}

public class CompanyDataRequest : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string RequestedByUserId { get; set; } = default!;
    public string Kind { get; set; } = default!;
    public string Status { get; set; } = "Requested";
    public string? SummaryJson { get; set; }
    public DateTime? CompletedAt { get; set; }
}

using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>Per-tenant Sales Agent runtime settings (limits, ICP defaults, pause).</summary>
public class SalesAgentSettings : AuditableEntity
{
    public Guid TenantId { get; set; }
    public bool IsPaused { get; set; }
    public int DailyResearchLimit { get; set; } = 10;
    public int MaxOutreachPerDay { get; set; } = 20;
    public bool AllowFixtureResearch { get; set; }
    public string IdealCustomerProfileJson { get; set; } = "{}";
    public string QualificationCriteriaJson { get; set; } = "[]";
    public int QualificationThreshold { get; set; } = 60;
    public string? ResearchProviderKey { get; set; }
    public string? ResearchApiKeyProtected { get; set; }
    public string? ResearchBaseUrl { get; set; }
    public string? CurrentObjective { get; set; }
    public Guid? ActiveObjectiveId { get; set; }
    public bool RequireOutreachApproval { get; set; } = true;
}

public class SalesAgentObjective : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string ObjectiveText { get; set; } = default!;
    public string Status { get; set; } = "Active";
    public int TargetProspectCount { get; set; }
    public string IdealCustomerProfileJson { get; set; } = "{}";
    public string? AssignedByUserId { get; set; }
    public string? DelegatedByAgentKey { get; set; }
    public Guid? ParentExecutionId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public int DiscoveredCount { get; set; }
    public int QualifiedCount { get; set; }
    public int OutreachDraftCount { get; set; }
    public int OutreachSentCount { get; set; }
    public int ResponseCount { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ResultSummary { get; set; }
    public string? ResultJson { get; set; }
}

public class SalesProspectResearch : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? ObjectiveId { get; set; }
    public Guid? LeadId { get; set; }
    public string CompanyName { get; set; } = default!;
    public string? Website { get; set; }
    public string? Industry { get; set; }
    public string? Country { get; set; }
    public string? CompanySize { get; set; }
    public string? BusinessDescription { get; set; }
    public string? PublicContactEmail { get; set; }
    public string? PublicContactPhone { get; set; }
    public string? DecisionMakerName { get; set; }
    public string? DecisionMakerTitle { get; set; }
    public string? SourceUrl { get; set; }
    public string ProviderKey { get; set; } = default!;
    public string SourceAttribution { get; set; } = default!;
    public DateTime ResearchedAt { get; set; } = DateTime.UtcNow;
    public int? LeadScore { get; set; }
    public string? ScoreExplanation { get; set; }
    public bool ScoreIsEstimate { get; set; } = true;
    public bool IsQualified { get; set; }
    public bool IsDuplicate { get; set; }
    public string DedupKey { get; set; } = default!;
    public string QualityStatus { get; set; } = "Valid";
    public string? QualityNotes { get; set; }
    public string RawPayloadJson { get; set; } = "{}";
}

public class SalesOutreachCampaign : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? ObjectiveId { get; set; }
    public string Name { get; set; } = default!;
    public string Status { get; set; } = "Draft";
    public string? Description { get; set; }
    public string SequenceJson { get; set; } = "[]";
    public int DraftCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int SentCount { get; set; }
    public int ResponseCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ReportJson { get; set; }
}

public class SalesOutreachMessage : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? ProspectResearchId { get; set; }
    public Guid? WorkItemId { get; set; }
    public Guid? ApprovalId { get; set; }
    public string ToEmail { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string FitExplanation { get; set; } = default!;
    public int SequenceStep { get; set; } = 1;
    public string Status { get; set; } = "Draft";
    public DateTime? ApprovedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ResponseAt { get; set; }
    public string? ResponseSummary { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? LastError { get; set; }
    public bool UsedTestSink { get; set; }
}

public class SalesEmailSuppression : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = default!;
    public string Reason { get; set; } = "Unsubscribe";
    public string? Source { get; set; }
}

public class SalesOutboundEmailLog : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? OutreachMessageId { get; set; }
    public Guid? ApprovalId { get; set; }
    public string ToEmail { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string ProviderKey { get; set; } = default!;
    public bool Performed { get; set; }
    public bool IsTestSink { get; set; }
    public string Status { get; set; } = default!;
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
}

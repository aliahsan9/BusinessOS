using System.Text.Json.Nodes;

namespace BusinessOS.Application.Features.SalesAgent;

public static class SalesAgentStatuses
{
    public const string Active = "Active";
    public const string Paused = "Paused";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Replied = "Replied";
    public const string Suppressed = "Suppressed";
}

public sealed record LeadResearchQuery(
    string? Industry,
    string? Country,
    string? RequiredServices,
    string? CompanySize,
    string? Keywords,
    int Limit);

public sealed record LeadResearchResult(
    string CompanyName,
    string? Website,
    string? Industry,
    string? Country,
    string? CompanySize,
    string? BusinessDescription,
    string? PublicContactEmail,
    string? PublicContactPhone,
    string? DecisionMakerName,
    string? DecisionMakerTitle,
    string? SourceUrl,
    string ProviderKey,
    string SourceAttribution,
    DateTime ResearchedAt,
    string RawPayloadJson,
    bool ContactVerified);

public sealed record LeadResearchProviderStatus(
    string ProviderKey,
    string DisplayName,
    bool IsAvailable,
    bool IsFixture,
    bool IsLiveIntegration,
    string Message);

public interface ILeadResearchProvider
{
    string ProviderKey { get; }
    string DisplayName { get; }
    bool IsFixture { get; }
    bool IsLiveIntegration { get; }
    Task<LeadResearchProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadResearchResult>> SearchAsync(LeadResearchQuery query, CancellationToken cancellationToken = default);
}

public interface ILeadResearchRouter
{
    Task<LeadResearchProviderStatus> GetActiveStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadResearchProviderStatus>> ListProvidersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadResearchResult>> SearchAsync(LeadResearchQuery query, CancellationToken cancellationToken = default);
}

public sealed record QualificationCriterion(string Key, string Label, string? ExpectedValue, int Weight);

public sealed record LeadQualificationResult(
    int Score,
    bool MeetsThreshold,
    string Explanation,
    bool ScoreIsEstimate,
    IReadOnlyList<string> MatchedCriteria,
    IReadOnlyList<string> MissingCriteria);

public interface ILeadQualificationService
{
    LeadQualificationResult Score(
        IReadOnlyList<QualificationCriterion> criteria,
        int threshold,
        string? industry,
        string? country,
        string? companySize,
        string? businessDescription,
        string? requiredServices,
        string? website,
        string? publicContactEmail);
}

public interface IOutboundEmailSink
{
    bool IsTestSink { get; }
    Task<(bool Performed, string Status, string Message, string? ProviderMessageId)> SendAsync(
        string toEmail,
        string subject,
        string body,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}

public sealed class AssignSalesObjectiveRequest
{
    public string Objective { get; set; } = "";
    public int? TargetProspectCount { get; set; }
    public string? IdealCustomerProfileJson { get; set; }
    public string? DelegatedByAgentKey { get; set; }
    public Guid? ParentExecutionId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public bool QueueExecution { get; set; } = true;
}

public sealed class SalesAgentSettingsWriteRequest
{
    public int? DailyResearchLimit { get; set; }
    public int? MaxOutreachPerDay { get; set; }
    public bool? AllowFixtureResearch { get; set; }
    public string? IdealCustomerProfileJson { get; set; }
    public string? QualificationCriteriaJson { get; set; }
    public int? QualificationThreshold { get; set; }
    public string? ResearchProviderKey { get; set; }
    public string? ResearchApiKey { get; set; }
    public string? ResearchBaseUrl { get; set; }
    public bool? RequireOutreachApproval { get; set; }
    public bool ClearResearchApiKey { get; set; }
}

public sealed class ImportAuthorizedProspectsRequest
{
    public Guid? ObjectiveId { get; set; }
    public List<AuthorizedProspectDto> Prospects { get; set; } = [];
}

public sealed class AuthorizedProspectDto
{
    public string CompanyName { get; set; } = "";
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
}

public sealed class SimulateProspectReplyRequest
{
    public Guid OutreachMessageId { get; set; }
    public string ReplySummary { get; set; } = "";
    public bool ScheduleFollowUp { get; set; } = true;
    public int FollowUpDays { get; set; } = 3;
}

public sealed record SalesAgentCommandCenterDto(
    string AgentStatus,
    bool IsPaused,
    string? CurrentObjective,
    Guid? ActiveObjectiveId,
    string? CurrentExecutionSummary,
    int LeadsDiscovered,
    int QualifiedLeads,
    int PendingOutreachApprovals,
    int EmailsSent,
    int ResponsesReceived,
    int MeetingsScheduled,
    int FailedTasks,
    decimal EstimatedCostUsd,
    bool CostIsEstimate,
    LeadResearchProviderStatus ResearchProvider,
    bool EmailIntegrationConnected,
    string EmailIntegrationMessage,
    IReadOnlyList<SalesAgentObjectiveDto> Objectives,
    IReadOnlyList<SalesProspectDto> RecentProspects,
    IReadOnlyList<SalesOutreachMessageDto> PendingApprovals,
    IReadOnlyList<SalesAgentExecutionDto> ExecutionHistory,
    SalesAgentSettingsDto Settings);

public sealed record SalesAgentSettingsDto(
    bool IsPaused,
    int DailyResearchLimit,
    int MaxOutreachPerDay,
    bool AllowFixtureResearch,
    string IdealCustomerProfileJson,
    string QualificationCriteriaJson,
    int QualificationThreshold,
    string? ResearchProviderKey,
    bool HasResearchApiKey,
    string? ResearchBaseUrl,
    bool RequireOutreachApproval,
    string? CurrentObjective,
    Guid? ActiveObjectiveId);

public sealed record SalesAgentObjectiveDto(
    Guid Id,
    string ObjectiveText,
    string Status,
    int TargetProspectCount,
    int DiscoveredCount,
    int QualifiedCount,
    int OutreachDraftCount,
    int OutreachSentCount,
    int ResponseCount,
    string? DelegatedByAgentKey,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ResultSummary);

public sealed record SalesProspectDto(
    Guid Id,
    Guid? LeadId,
    string CompanyName,
    string? Website,
    string? Industry,
    string? Country,
    string? CompanySize,
    string? PublicContactEmail,
    string ProviderKey,
    string SourceAttribution,
    DateTime ResearchedAt,
    int? LeadScore,
    string? ScoreExplanation,
    bool ScoreIsEstimate,
    bool IsQualified,
    bool IsDuplicate,
    string QualityStatus);

public sealed record SalesOutreachMessageDto(
    Guid Id,
    Guid CampaignId,
    Guid? LeadId,
    string ToEmail,
    string Subject,
    string Body,
    string FitExplanation,
    string Status,
    int SequenceStep,
    DateTime CreatedAt,
    DateTime? SentAt,
    bool UsedTestSink);

public sealed record SalesAgentExecutionDto(
    Guid Id,
    string Status,
    string Objective,
    string? Summary,
    DateTime StartedAt,
    DateTime? CompletedAt,
    decimal EstimatedCostUsd,
    string? Error);

public sealed record SalesCampaignReportDto(
    Guid? CampaignId,
    Guid? ObjectiveId,
    string Title,
    int ProspectsResearched,
    int QualifiedLeads,
    int OutreachDrafts,
    int EmailsSent,
    int Responses,
    int FollowUpsScheduled,
    int FailedSends,
    decimal EstimatedCostUsd,
    bool CostIsEstimate,
    string Summary,
    JsonObject Details);

public interface ISalesAgentService
{
    Task<SalesAgentCommandCenterDto> GetCommandCenterAsync(CancellationToken cancellationToken = default);
    Task<SalesAgentSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<SalesAgentSettingsDto> SaveSettingsAsync(SalesAgentSettingsWriteRequest request, CancellationToken cancellationToken = default);
    Task<SalesAgentObjectiveDto> AssignObjectiveAsync(AssignSalesObjectiveRequest request, CancellationToken cancellationToken = default);
    Task<SalesAgentSettingsDto> PauseAsync(CancellationToken cancellationToken = default);
    Task<SalesAgentSettingsDto> ResumeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesProspectDto>> ImportAuthorizedProspectsAsync(ImportAuthorizedProspectsRequest request, CancellationToken cancellationToken = default);
    Task<SalesCampaignReportDto> GetCampaignReportAsync(Guid? objectiveId, Guid? campaignId, CancellationToken cancellationToken = default);
    Task<SalesOutreachMessageDto> SimulateProspectReplyAsync(SimulateProspectReplyRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadResearchProviderStatus>> GetResearchProvidersAsync(CancellationToken cancellationToken = default);
}

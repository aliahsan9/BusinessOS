using BusinessOS.Domain.Companies;

namespace BusinessOS.Application.Features.Launch;

public interface IAgencyLaunchService
{
    LaunchNoticeDto GetNotice();
    Task<AgencySetupDto> GetSetupAsync(CancellationToken cancellationToken = default);
    Task<AgencySetupDto> SaveCompanyAsync(AgencyCompanyWrite request, CancellationToken cancellationToken = default);
    Task<AgencySetupDto> SaveProfileAsync(AgencyProfileWrite request, CancellationToken cancellationToken = default);
    Task<AgencySetupDto> SaveWorkspaceAsync(AgencyWorkspaceWrite request, CancellationToken cancellationToken = default);
    Task<AgencySetupDto> SaveWorkforceAsync(AgencyWorkforceWrite request, CancellationToken cancellationToken = default);
    Task<AgencySetupDto> SavePlanAsync(AgencyPlanWrite request, CancellationToken cancellationToken = default);
    Task<AgencySetupDto> SaveObjectiveAsync(AgencyObjectiveWrite request, CancellationToken cancellationToken = default);
    Task<InvitationRedeemDto> RedeemInvitationAsync(string code, CancellationToken cancellationToken = default);
    Task<LaunchFeedbackDto> SubmitFeedbackAsync(LaunchFeedbackWrite request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LaunchFeedbackDto>> ListFeedbackAsync(CancellationToken cancellationToken = default);
    Task RecordUsageAsync(string eventKey, CancellationToken cancellationToken = default);
    Task<CompanyAnalyticsDto> GetCompanyAnalyticsAsync(CancellationToken cancellationToken = default);
    Task<CompanyDataRequestDto> ExportAsync(CancellationToken cancellationToken = default);
    Task<CompanyDataRequestDto?> LatestExportAsync(CancellationToken cancellationToken = default);
    Task<CompanyDataRequestDto> RequestDeletionAsync(CancellationToken cancellationToken = default);
    Task<EarlyAccessInvitationDto> CreateInvitationAsync(CreateInvitationWrite request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EarlyAccessInvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken = default);
    Task RevokeInvitationAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetInvitationRequiredAsync(bool required, CancellationToken cancellationToken = default);
    Task<PlatformAnalyticsDto> GetPlatformAnalyticsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LaunchFeedbackDto>> ListPlatformFeedbackAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BacklogItemDto>> ListBacklogAsync(CancellationToken cancellationToken = default);
    Task<BacklogItemDto> UpdateBacklogAsync(Guid id, BacklogUpdate request, CancellationToken cancellationToken = default);
    Task<CompanyDataRequestDto> ConfirmDeletionAsync(Guid companyId, CancellationToken cancellationToken = default);
}

public sealed record LaunchNoticeDto(
    string Product,
    string Audience,
    string Status,
    IReadOnlyList<string> Available,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> NotInThisRelease,
    string DataHandling,
    string SubscriptionTerms,
    string Support);

public sealed record AgencyTemplateDto(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Navigation,
    IReadOnlyList<string> Widgets,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Workflows,
    IReadOnlyList<string> Agents);

public sealed record LaunchPlanDto(
    string Key,
    string Name,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int TrialDays,
    int MaxAiRequests,
    int MaxAgentExecutions,
    int MaxAutomationRuns,
    string Currency,
    string CheckoutNote);

public sealed class AgencyCompanyWrite
{
    public string Name { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string Country { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public string Timezone { get; set; } = "UTC";
}

public sealed class AgencyProfileWrite
{
    public string TemplateKey { get; set; } = "";
    public string? Services { get; set; }
    public string? Technologies { get; set; }
    public string? TeamSize { get; set; }
    public string? TargetClients { get; set; }
    public string? Goals { get; set; }
}

public sealed class AgencyWorkspaceWrite
{
    public List<string> Modules { get; set; } = [];
    public List<string> Emails { get; set; } = [];
    public List<string> Roles { get; set; } = [];
    public string? InitialProjectName { get; set; }
}

public sealed class AgencyWorkforceWrite
{
    public List<string> AgentKeys { get; set; } = [];
    public string ApprovalPreference { get; set; } = "RequireApproval";
    public decimal? MonthlyBudgetUsd { get; set; }
}

public sealed class AgencyPlanWrite
{
    public string PlanKey { get; set; } = "";
}

public sealed class AgencyObjectiveWrite
{
    public string Objective { get; set; } = "";
}

public sealed class LaunchFeedbackWrite
{
    public string Kind { get; set; } = "";
    public string? Subject { get; set; }
    public string Message { get; set; } = "";
    public int? Rating { get; set; }
    public string? Page { get; set; }
}

public sealed class CreateInvitationWrite
{
    public string Email { get; set; } = "";
    public string AgencyName { get; set; } = "";
    public int? ValidDays { get; set; }
}

public sealed class BacklogUpdate
{
    public int Priority { get; set; }
    public string Status { get; set; } = "";
}

public sealed record AgencySetupDto(
    LaunchNoticeDto Notice,
    bool InvitationRequired,
    bool InvitationRedeemed,
    AgencyWorkspaceDto Workspace,
    IReadOnlyList<AgencyTemplateDto> Templates,
    IReadOnlyList<LaunchPlanDto> Plans);

public sealed record AgencyWorkspaceDto(
    int CurrentStep,
    bool IsCompleted,
    bool CompanySaved,
    bool ProfileSaved,
    bool WorkspaceSaved,
    bool WorkforceSaved,
    bool PlanSaved,
    string? TemplateKey,
    string? Services,
    string? Technologies,
    string? TeamSize,
    string? TargetClients,
    string? Goals,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> InvitedEmails,
    IReadOnlyList<string> Roles,
    string? InitialProjectName,
    Guid? InitialProjectId,
    IReadOnlyList<string> Agents,
    string ApprovalPreference,
    decimal? MonthlyAiBudgetUsd,
    string? SelectedPlanKey,
    string? FirstObjective,
    Guid? FirstTaskId,
    bool FirstTaskQueued,
    int InvitesCreated,
    string? InviteNote);

public sealed record InvitationRedeemDto(bool Redeemed, string AgencyName);

public sealed record LaunchFeedbackDto(
    Guid Id,
    string Kind,
    string Subject,
    string Message,
    int? Rating,
    string? Page,
    string Status,
    int Priority,
    DateTime CreatedAt);

public sealed record EarlyAccessInvitationDto(
    Guid Id,
    string Code,
    string Email,
    string AgencyName,
    string Status,
    DateTime ExpiresAt,
    Guid? RedeemedCompanyId,
    DateTime CreatedAt);

public sealed record BacklogItemDto(
    Guid Id,
    Guid? SourceCompanyId,
    string Title,
    string Kind,
    int Priority,
    string Status,
    DateTime CreatedAt);

public sealed record CompanyDataRequestDto(
    Guid Id,
    string Kind,
    string Status,
    string? SummaryJson,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string Procedure);

public sealed record CompanyAnalyticsDto(
    bool AgencyOnboardingCompleted,
    int UsageEvents,
    IReadOnlyList<NamedCountDto> UsageByEvent,
    int OpenFeedback,
    int SupportRequests,
    int AgentExecutions,
    int FailedExecutions,
    int WorkflowRuns,
    IReadOnlyList<NamedCountDto> AgentUsage);

public sealed record PlatformAnalyticsDto(
    int Companies,
    int AgencyOnboardingsCompleted,
    int LegacyOnboardingsCompleted,
    int ActiveCompanies,
    int WeeklyActiveCompanies,
    int WeeklyActiveUsers,
    int AgentExecutions,
    int FailedExecutions,
    int WorkflowRuns,
    int TrialSubscriptions,
    int ActiveSubscriptions,
    int CancelledSubscriptions,
    int SupportRequests,
    int OpenFeedback,
    IReadOnlyList<NamedCountDto> UsageByEvent,
    IReadOnlyList<NamedCountDto> AgentUsage);

public sealed record NamedCountDto(string Name, int Count);

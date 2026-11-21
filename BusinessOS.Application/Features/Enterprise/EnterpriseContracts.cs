namespace BusinessOS.Application.Features.Enterprise;

public sealed record OrgScopeSnapshot(
    bool IsTenantWide,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<Guid> DepartmentIds);

public interface IOrgScopeService
{
    Task<OrgScopeSnapshot> GetCurrentScopeAsync(CancellationToken cancellationToken = default);
    Task EnsureCanAccessBranchAsync(Guid? branchId, CancellationToken cancellationToken = default);
    Task EnsureCanAccessDepartmentAsync(Guid? departmentId, CancellationToken cancellationToken = default);
    bool IsVisible(OrgScopeSnapshot scope, Guid? branchId, Guid? departmentId);
}

public interface IEnterpriseEntitlementGuard
{
    Task EnsureAdvancedSecurityAsync(CancellationToken cancellationToken = default);
    Task EnsureMultiBranchAsync(CancellationToken cancellationToken = default);
}

public interface IEnterpriseOrganizationService
{
    Task<IReadOnlyList<CompanyBranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default);
    Task<CompanyBranchDto> SaveBranchAsync(Guid? id, CompanyBranchWriteRequest request, CancellationToken cancellationToken = default);
    Task<OrgHierarchyDto> GetHierarchyAsync(CancellationToken cancellationToken = default);
}

public interface IEnterpriseRbacService
{
    Task<IReadOnlyList<PermissionGroupDto>> GetPermissionGroupsAsync(CancellationToken cancellationToken = default);
    Task<PermissionGroupDto> SavePermissionGroupAsync(Guid? id, PermissionGroupWriteRequest request, CancellationToken cancellationToken = default);
    Task<TemporaryPermissionGrantDto> GrantTemporaryAsync(TemporaryPermissionWriteRequest request, CancellationToken cancellationToken = default);
    Task RevokeTemporaryAsync(Guid grantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemporaryPermissionGrantDto>> GetTemporaryGrantsAsync(string? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleAssignmentHistoryDto>> GetAssignmentHistoryAsync(string? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ResolveEffectivePermissionsAsync(string userId, CancellationToken cancellationToken = default);
}

public interface IApprovalGovernanceService
{
    Task<IReadOnlyList<ApprovalPolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default);
    Task<ApprovalPolicyDto> SavePolicyAsync(Guid? id, ApprovalPolicyWriteRequest request, CancellationToken cancellationToken = default);
    Task<ApprovalRequestDto?> EvaluateAndEnqueueAsync(ApprovalEvaluateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApprovalRequestDto>> GetRequestsAsync(string? status = null, CancellationToken cancellationToken = default);
    Task<ApprovalRequestDto> DecideAsync(Guid requestId, ApprovalDecisionWriteRequest request, CancellationToken cancellationToken = default);
    Task<ApprovalRequestDto> DelegateAsync(Guid requestId, ApprovalDelegateRequest request, CancellationToken cancellationToken = default);
    Task EscalateOverdueAsync(CancellationToken cancellationToken = default);
}

public interface IEnterpriseAuditService
{
    Task LogAsync(EnterpriseAuditWriteRequest request, CancellationToken cancellationToken = default);
    Task<PagedEnterpriseAuditDto> QueryAsync(EnterpriseAuditQuery query, CancellationToken cancellationToken = default);
    Task<byte[]> ExportCsvAsync(EnterpriseAuditQuery query, CancellationToken cancellationToken = default);
    Task PurgeExpiredAsync(CancellationToken cancellationToken = default);
}

public interface IEnterpriseReportingService
{
    Task<EnterpriseReportDto> GetBranchReportAsync(Guid? branchId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<EnterpriseReportDto> GetDepartmentReportAsync(Guid? departmentId, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<EnterpriseReportDto> GetApprovalAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<EnterpriseReportDto> GetAgentProductivityAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<EnterpriseReportDto> GetConsolidatedFinanceAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
}

public interface IEnterpriseSsoService
{
    Task<IReadOnlyList<TenantSsoConfigurationDto>> GetConfigurationsAsync(CancellationToken cancellationToken = default);
    Task<TenantSsoConfigurationDto> SaveConfigurationAsync(Guid? id, TenantSsoWriteRequest request, CancellationToken cancellationToken = default);
    Task<EnterpriseSecuritySettingsDto> GetSecuritySettingsAsync(CancellationToken cancellationToken = default);
    Task<EnterpriseSecuritySettingsDto> SaveSecuritySettingsAsync(EnterpriseSecuritySettingsWriteRequest request, CancellationToken cancellationToken = default);
    Task<SsoChallengeResult> BeginOidcAsync(Guid configurationId, string returnUrl, CancellationToken cancellationToken = default);
    Task<Auth.DTOs.AuthResponse> CompleteOidcAsync(SsoOidcCallbackRequest request, CancellationToken cancellationToken = default);
    Task<SsoChallengeResult> BeginSamlAsync(Guid configurationId, string returnUrl, CancellationToken cancellationToken = default);
    Task<Auth.DTOs.AuthResponse> CompleteSamlAsync(SsoSamlCallbackRequest request, CancellationToken cancellationToken = default);
}

public interface IEnterpriseMfaService
{
    Task<MfaSetupDto> BeginSetupAsync(CancellationToken cancellationToken = default);
    Task EnableAsync(string code, CancellationToken cancellationToken = default);
    Task DisableAsync(string code, CancellationToken cancellationToken = default);
    Task<Auth.DTOs.AuthResponse> VerifyLoginAsync(string mfaToken, string code, CancellationToken cancellationToken = default);
}

public interface IEnterpriseAgentGovernanceService
{
    Task<AgentGovernanceDto> GetAsync(string agentKey, CancellationToken cancellationToken = default);
    Task<AgentGovernanceDto> SaveAsync(string agentKey, AgentGovernanceWriteRequest request, CancellationToken cancellationToken = default);
    Task EnsureToolAllowedAsync(string agentKey, string toolName, Guid? departmentId, CancellationToken cancellationToken = default);
}

public sealed record CompanyBranchDto(Guid Id, string Name, string? Code, string? Region, bool IsHeadquarters, Guid? ManagerEmployeeId, string? Address, bool IsActive);
public sealed class CompanyBranchWriteRequest
{
    public string Name { get; set; } = default!;
    public string? Code { get; set; }
    public string? Region { get; set; }
    public bool IsHeadquarters { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record OrgHierarchyDto(
    IReadOnlyList<CompanyBranchDto> Branches,
    IReadOnlyList<OrgDepartmentNodeDto> Departments,
    IReadOnlyList<OrgTeamNodeDto> Teams);

public sealed record OrgDepartmentNodeDto(Guid Id, string Name, Guid? BranchId, Guid? ManagerEmployeeId, bool IsActive);
public sealed record OrgTeamNodeDto(Guid Id, string Name, Guid? DepartmentId, Guid? LeaderEmployeeId, bool IsActive);

public sealed record PermissionGroupDto(Guid Id, string Name, string? Description, IReadOnlyList<string> PermissionCodes, bool IsActive);
public sealed class PermissionGroupWriteRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public IReadOnlyList<string> PermissionCodes { get; set; } = [];
    public bool IsActive { get; set; } = true;
}

public sealed record TemporaryPermissionGrantDto(Guid Id, string UserId, string PermissionCode, Guid? RoleId, DateTime ExpiresAt, string GrantedByUserId, string? Reason, bool IsRevoked);
public sealed class TemporaryPermissionWriteRequest
{
    public string UserId { get; set; } = default!;
    public string PermissionCode { get; set; } = default!;
    public Guid? RoleId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Reason { get; set; }
}

public sealed record RoleAssignmentHistoryDto(Guid Id, string UserId, Guid? RoleId, string? RoleName, string? PermissionCode, string Action, string ActorUserId, string? Reason, DateTime? ExpiresAt, DateTime CreatedAt);

public sealed record ApprovalPolicyStepDto(int StepOrder, string ApproverType, string? ApproverRoleName, string? ApproverPermissionCode, string? ApproverUserId, bool RequireDepartmentHead);
public sealed record ApprovalPolicyDto(Guid Id, string Name, string? Description, string TriggerType, decimal? AmountThreshold, string Mode, int DeadlineHours, string? EscalateToUserId, string? EscalateToRoleName, bool IsActive, Guid? BranchId, Guid? DepartmentId, string? ToolName, IReadOnlyList<ApprovalPolicyStepDto> Steps);
public sealed class ApprovalPolicyWriteRequest
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string TriggerType { get; set; } = default!;
    public decimal? AmountThreshold { get; set; }
    public string Mode { get; set; } = "Sequential";
    public int DeadlineHours { get; set; } = 72;
    public string? EscalateToUserId { get; set; }
    public string? EscalateToRoleName { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? ToolName { get; set; }
    public IReadOnlyList<ApprovalPolicyStepDto> Steps { get; set; } = [];
}

public sealed class ApprovalEvaluateRequest
{
    public string TriggerType { get; set; } = default!;
    public string SubjectType { get; set; } = default!;
    public string SubjectId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? PayloadJson { get; set; }
    public decimal? Amount { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? ToolName { get; set; }
    public Guid? NalvaApprovalId { get; set; }
}

public sealed record ApprovalDecisionDto(Guid Id, int StepOrder, string Decision, string DecidedByUserId, string? DelegatedFromUserId, string? Note, DateTime DecidedAt);
public sealed record ApprovalRequestDto(Guid Id, string TriggerType, string SubjectType, string SubjectId, string Title, decimal? Amount, string Status, string Mode, int CurrentStepOrder, DateTime RequestedAt, string RequestedByUserId, DateTime? DeadlineAt, string? RejectionReason, Guid? NalvaApprovalId, IReadOnlyList<ApprovalDecisionDto> Decisions);
public sealed class ApprovalDecisionWriteRequest
{
    public bool Approve { get; set; }
    public string? Note { get; set; }
    public string? RejectionReason { get; set; }
}
public sealed class ApprovalDelegateRequest
{
    public string ToUserId { get; set; } = default!;
    public string? Note { get; set; }
}

public sealed class EnterpriseAuditWriteRequest
{
    public string Category { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string? ActorUserId { get; set; }
    public string? ActorType { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? BranchId { get; set; }
    public string? DepartmentId { get; set; }
    public string? IpAddress { get; set; }
    public string? DetailsJson { get; set; }
    public bool IsSensitive { get; set; }
}

public sealed class EnterpriseAuditQuery
{
    public string? Category { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed record EnterpriseAuditEventDto(Guid Id, string Category, string Action, string? ActorUserId, string? ActorType, string? EntityType, string? EntityId, string? DetailsJson, bool IsSensitive, DateTime CreatedAt);
public sealed record PagedEnterpriseAuditDto(IReadOnlyList<EnterpriseAuditEventDto> Items, int Total, int Page, int PageSize);

public sealed record EnterpriseReportMetric(string Key, string Label, decimal Value, string? Unit = null);
public sealed record EnterpriseReportDto(string Title, DateTime GeneratedAt, IReadOnlyList<EnterpriseReportMetric> Metrics, IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

public sealed record TenantSsoConfigurationDto(Guid Id, string Protocol, string Provider, bool IsEnabled, string? DisplayName, string? Authority, string? ClientId, string? AllowedEmailDomains, string? MetadataUrl, string? IdpEntityId, string? SpEntityId, string? DefaultRoleName, bool JitProvision, bool HasClientSecret);
public sealed class TenantSsoWriteRequest
{
    public string Protocol { get; set; } = "Oidc";
    public string Provider { get; set; } = default!;
    public bool IsEnabled { get; set; }
    public string? DisplayName { get; set; }
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? AllowedEmailDomains { get; set; }
    public string? MetadataUrl { get; set; }
    public string? IdpEntityId { get; set; }
    public string? IdpMetadataXml { get; set; }
    public string? SpEntityId { get; set; }
    public string? DefaultRoleName { get; set; }
    public string? GroupRoleMapJson { get; set; }
    public bool JitProvision { get; set; } = true;
}

public sealed record EnterpriseSecuritySettingsDto(bool RequireMfa, int SessionIdleMinutes, int MaxConcurrentSessions, int AuditRetentionDays, bool EnforceOrgScopeOnQueries);
public sealed class EnterpriseSecuritySettingsWriteRequest
{
    public bool RequireMfa { get; set; }
    public int SessionIdleMinutes { get; set; } = 60;
    public int MaxConcurrentSessions { get; set; } = 10;
    public int AuditRetentionDays { get; set; } = 365;
    public bool EnforceOrgScopeOnQueries { get; set; } = true;
}

public sealed record SsoChallengeResult(string RedirectUrl, string State);
public sealed class SsoOidcCallbackRequest
{
    public Guid ConfigurationId { get; set; }
    public string Code { get; set; } = default!;
    public string State { get; set; } = default!;
    public string? CodeVerifier { get; set; }
}
public sealed class SsoSamlCallbackRequest
{
    public Guid ConfigurationId { get; set; }
    public string SamlResponse { get; set; } = default!;
    public string? RelayState { get; set; }
}

public sealed record MfaSetupDto(string SharedKey, string AuthenticatorUri);
public sealed record AgentGovernanceDto(string AgentKey, Guid? DepartmentId, Guid? BranchId, IReadOnlyList<string> DeniedTools, IReadOnlyList<string> SensitiveDataRestrictions, bool RequireHumanSupervision, int DailyExecutionLimit, int MonthlyTokenBudget);
public sealed class AgentGovernanceWriteRequest
{
    public Guid? DepartmentId { get; set; }
    public Guid? BranchId { get; set; }
    public IReadOnlyList<string> DeniedTools { get; set; } = [];
    public IReadOnlyList<string> SensitiveDataRestrictions { get; set; } = [];
    public bool RequireHumanSupervision { get; set; }
    public int? DailyExecutionLimit { get; set; }
    public int? MonthlyTokenBudget { get; set; }
}

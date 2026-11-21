using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>Organizational branch (HQ or regional). Enterprise / MultiBranch feature.</summary>
public class CompanyBranch : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? Code { get; set; }
    public string? Region { get; set; }
    public bool IsHeadquarters { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PermissionGroup : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<PermissionGroupPermission> Permissions { get; set; } = new List<PermissionGroupPermission>();
}

public class PermissionGroupPermission
{
    public Guid PermissionGroupId { get; set; }
    public Guid PermissionId { get; set; }
    public Guid TenantId { get; set; }
    public PermissionGroup PermissionGroup { get; set; } = default!;
    public Permission Permission { get; set; } = default!;
}

public class TemporaryPermissionGrant : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;
    public string PermissionCode { get; set; } = default!;
    public Guid? RoleId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string GrantedByUserId { get; set; } = default!;
    public string? Reason { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByUserId { get; set; }
}

public class RoleAssignmentHistory : BaseEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = default!;
    public Guid? RoleId { get; set; }
    public string? RoleName { get; set; }
    public string? PermissionCode { get; set; }
    public string Action { get; set; } = default!;
    public string ActorUserId { get; set; } = default!;
    public string? Reason { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ApprovalPolicy : AuditableEntity
{
    public Guid TenantId { get; set; }
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
    public ICollection<ApprovalPolicyStep> Steps { get; set; } = new List<ApprovalPolicyStep>();
}

public class ApprovalPolicyStep : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ApprovalPolicyId { get; set; }
    public int StepOrder { get; set; }
    public string ApproverType { get; set; } = "Role";
    public string? ApproverRoleName { get; set; }
    public string? ApproverPermissionCode { get; set; }
    public string? ApproverUserId { get; set; }
    public bool RequireDepartmentHead { get; set; }
    public ApprovalPolicy ApprovalPolicy { get; set; } = default!;
}

public class ApprovalRequest : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? ApprovalPolicyId { get; set; }
    public string TriggerType { get; set; } = default!;
    public string SubjectType { get; set; } = default!;
    public string SubjectId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? PayloadJson { get; set; }
    public decimal? Amount { get; set; }
    public string Status { get; set; } = "Pending";
    public string Mode { get; set; } = "Sequential";
    public int CurrentStepOrder { get; set; } = 1;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string RequestedByUserId { get; set; } = default!;
    public DateTime? DeadlineAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public string? EscalatedToUserId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? NalvaApprovalId { get; set; }
    public string? RejectionReason { get; set; }
    public ICollection<ApprovalDecision> Decisions { get; set; } = new List<ApprovalDecision>();
}

public class ApprovalDecision : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ApprovalRequestId { get; set; }
    public int StepOrder { get; set; }
    public string Decision { get; set; } = default!;
    public string DecidedByUserId { get; set; } = default!;
    public string? DelegatedFromUserId { get; set; }
    public string? Note { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
    public ApprovalRequest ApprovalRequest { get; set; } = default!;
}

public class EnterpriseAuditEvent : BaseEntity
{
    public Guid TenantId { get; set; }
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TenantSsoConfiguration : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Protocol { get; set; } = "Oidc";
    public string Provider { get; set; } = default!;
    public bool IsEnabled { get; set; }
    public string? DisplayName { get; set; }
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecretProtected { get; set; }
    public string? AllowedEmailDomains { get; set; }
    public string? MetadataUrl { get; set; }
    public string? IdpEntityId { get; set; }
    public string? IdpMetadataXml { get; set; }
    public string? SpEntityId { get; set; }
    public string? DefaultRoleName { get; set; }
    public string? GroupRoleMapJson { get; set; }
    public bool JitProvision { get; set; } = true;
}

public class TenantEnterpriseSecuritySettings : AuditableEntity
{
    public Guid TenantId { get; set; }
    public bool RequireMfa { get; set; }
    public int SessionIdleMinutes { get; set; } = 60;
    public int MaxConcurrentSessions { get; set; } = 10;
    public int AuditRetentionDays { get; set; } = 365;
    public bool EnforceOrgScopeOnQueries { get; set; } = true;
}

public static class ApprovalTriggerTypes
{
    public const string ExpenseAmount = "ExpenseAmount";
    public const string PurchaseOrder = "PurchaseOrder";
    public const string FinancialTransfer = "FinancialTransfer";
    public const string MarketingPublish = "MarketingPublish";
    public const string EmployeeSensitiveChange = "EmployeeSensitiveChange";
    public const string AgentTool = "AgentTool";
}

public static class RoleScopeKinds
{
    public const string Tenant = "Tenant";
    public const string Branch = "Branch";
    public const string Department = "Department";
}

public static class EnterpriseAuditCategories
{
    public const string Authentication = "Authentication";
    public const string Permission = "Permission";
    public const string Financial = "Financial";
    public const string DataExport = "DataExport";
    public const string SensitiveAccess = "SensitiveAccess";
    public const string Agent = "Agent";
    public const string Approval = "Approval";
    public const string Integration = "Integration";
    public const string Administrative = "Administrative";
}

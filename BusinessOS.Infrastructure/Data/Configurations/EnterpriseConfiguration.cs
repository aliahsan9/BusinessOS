using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public sealed class CompanyBranchConfiguration : IEntityTypeConfiguration<CompanyBranch>
{
    public void Configure(EntityTypeBuilder<CompanyBranch> builder)
    {
        builder.ToTable("CompanyBranches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Region).HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[Code] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.IsHeadquarters });
    }
}

public sealed class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>
{
    public void Configure(EntityTypeBuilder<PermissionGroup> builder)
    {
        builder.ToTable("PermissionGroups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
    }
}

public sealed class PermissionGroupPermissionConfiguration : IEntityTypeConfiguration<PermissionGroupPermission>
{
    public void Configure(EntityTypeBuilder<PermissionGroupPermission> builder)
    {
        builder.ToTable("PermissionGroupPermissions");
        builder.HasKey(x => new { x.PermissionGroupId, x.PermissionId });
        builder.HasOne(x => x.PermissionGroup).WithMany(x => x.Permissions).HasForeignKey(x => x.PermissionGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TemporaryPermissionGrantConfiguration : IEntityTypeConfiguration<TemporaryPermissionGrant>
{
    public void Configure(EntityTypeBuilder<TemporaryPermissionGrant> builder)
    {
        builder.ToTable("TemporaryPermissionGrants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.PermissionCode).HasMaxLength(150).IsRequired();
        builder.Property(x => x.GrantedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.ExpiresAt });
    }
}

public sealed class RoleAssignmentHistoryConfiguration : IEntityTypeConfiguration<RoleAssignmentHistory>
{
    public void Configure(EntityTypeBuilder<RoleAssignmentHistory> builder)
    {
        builder.ToTable("RoleAssignmentHistories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.RoleName).HasMaxLength(100);
        builder.Property(x => x.PermissionCode).HasMaxLength(150);
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt });
    }
}

public sealed class ApprovalPolicyConfiguration : IEntityTypeConfiguration<ApprovalPolicy>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicy> builder)
    {
        builder.ToTable("ApprovalPolicies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.TriggerType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Mode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EscalateToUserId).HasMaxLength(450);
        builder.Property(x => x.EscalateToRoleName).HasMaxLength(100);
        builder.Property(x => x.ToolName).HasMaxLength(150);
        builder.HasMany(x => x.Steps).WithOne(x => x.ApprovalPolicy).HasForeignKey(x => x.ApprovalPolicyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.TriggerType, x.IsActive });
    }
}

public sealed class ApprovalPolicyStepConfiguration : IEntityTypeConfiguration<ApprovalPolicyStep>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicyStep> builder)
    {
        builder.ToTable("ApprovalPolicySteps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ApproverType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ApproverRoleName).HasMaxLength(100);
        builder.Property(x => x.ApproverPermissionCode).HasMaxLength(150);
        builder.Property(x => x.ApproverUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.ApprovalPolicyId, x.StepOrder }).IsUnique();
    }
}

public sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("ApprovalRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TriggerType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.SubjectType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SubjectId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Mode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.EscalatedToUserId).HasMaxLength(450);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.HasMany(x => x.Decisions).WithOne(x => x.ApprovalRequest).HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.DeadlineAt });
    }
}

public sealed class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("ApprovalDecisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Decision).HasMaxLength(40).IsRequired();
        builder.Property(x => x.DecidedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.DelegatedFromUserId).HasMaxLength(450);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ApprovalRequestId, x.StepOrder });
    }
}

public sealed class EnterpriseAuditEventConfiguration : IEntityTypeConfiguration<EnterpriseAuditEvent>
{
    public void Configure(EntityTypeBuilder<EnterpriseAuditEvent> builder)
    {
        builder.ToTable("EnterpriseAuditEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ActorUserId).HasMaxLength(450);
        builder.Property(x => x.ActorType).HasMaxLength(40);
        builder.Property(x => x.EntityType).HasMaxLength(120);
        builder.Property(x => x.EntityId).HasMaxLength(100);
        builder.Property(x => x.BranchId).HasMaxLength(64);
        builder.Property(x => x.DepartmentId).HasMaxLength(64);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.HasIndex(x => new { x.TenantId, x.Category, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.ActorUserId, x.CreatedAt });
    }
}

public sealed class TenantSsoConfigurationConfiguration : IEntityTypeConfiguration<TenantSsoConfiguration>
{
    public void Configure(EntityTypeBuilder<TenantSsoConfiguration> builder)
    {
        builder.ToTable("TenantSsoConfigurations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Protocol).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Authority).HasMaxLength(500);
        builder.Property(x => x.ClientId).HasMaxLength(200);
        builder.Property(x => x.ClientSecretProtected).HasMaxLength(4000);
        builder.Property(x => x.AllowedEmailDomains).HasMaxLength(1000);
        builder.Property(x => x.MetadataUrl).HasMaxLength(1000);
        builder.Property(x => x.IdpEntityId).HasMaxLength(500);
        builder.Property(x => x.SpEntityId).HasMaxLength(500);
        builder.Property(x => x.DefaultRoleName).HasMaxLength(100);
        builder.HasIndex(x => new { x.TenantId, x.Provider, x.Protocol });
    }
}

public sealed class TenantEnterpriseSecuritySettingsConfiguration : IEntityTypeConfiguration<TenantEnterpriseSecuritySettings>
{
    public void Configure(EntityTypeBuilder<TenantEnterpriseSecuritySettings> builder)
    {
        builder.ToTable("TenantEnterpriseSecuritySettings");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

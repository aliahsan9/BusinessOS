using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

[DbContext(typeof(BusinessOSDbContext))]
[Migration("20261121190000_Phase10Enterprise")]
public partial class Phase10Enterprise : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('dbo.Roles', 'ScopeKind') IS NULL
                ALTER TABLE [dbo].[Roles] ADD [ScopeKind] nvarchar(40) NOT NULL CONSTRAINT DF_Roles_ScopeKind DEFAULT N'Tenant';
            IF COL_LENGTH('dbo.Roles', 'ScopeId') IS NULL
                ALTER TABLE [dbo].[Roles] ADD [ScopeId] uniqueidentifier NULL;
            IF COL_LENGTH('dbo.Roles', 'IsReadOnly') IS NULL
                ALTER TABLE [dbo].[Roles] ADD [IsReadOnly] bit NOT NULL CONSTRAINT DF_Roles_IsReadOnly DEFAULT 0;

            IF COL_LENGTH('dbo.Departments', 'BranchId') IS NULL
                ALTER TABLE [dbo].[Departments] ADD [BranchId] uniqueidentifier NULL;
            IF COL_LENGTH('dbo.Warehouses', 'BranchId') IS NULL
                ALTER TABLE [dbo].[Warehouses] ADD [BranchId] uniqueidentifier NULL;
            IF COL_LENGTH('dbo.Employees', 'PrimaryBranchId') IS NULL
                ALTER TABLE [dbo].[Employees] ADD [PrimaryBranchId] uniqueidentifier NULL;
            IF COL_LENGTH('dbo.Employees', 'IsRegionalManager') IS NULL
                ALTER TABLE [dbo].[Employees] ADD [IsRegionalManager] bit NOT NULL CONSTRAINT DF_Employees_IsRegionalManager DEFAULT 0;

            IF COL_LENGTH('dbo.Expenses', 'BranchId') IS NULL
                ALTER TABLE [dbo].[Expenses] ADD [BranchId] uniqueidentifier NULL;
            IF COL_LENGTH('dbo.Expenses', 'DepartmentId') IS NULL
                ALTER TABLE [dbo].[Expenses] ADD [DepartmentId] uniqueidentifier NULL;
            IF COL_LENGTH('dbo.Expenses', 'PendingApprovalRequestId') IS NULL
                ALTER TABLE [dbo].[Expenses] ADD [PendingApprovalRequestId] uniqueidentifier NULL;
            IF COL_LENGTH('dbo.Orders', 'BranchId') IS NULL
                ALTER TABLE [dbo].[Orders] ADD [BranchId] uniqueidentifier NULL;

            IF COL_LENGTH('dbo.NalvaCompanyAgents', 'BranchId') IS NULL
                ALTER TABLE [dbo].[NalvaCompanyAgents] ADD [BranchId] uniqueidentifier NULL;
            IF COL_LENGTH('dbo.NalvaCompanyAgents', 'DeniedToolsJson') IS NULL
                ALTER TABLE [dbo].[NalvaCompanyAgents] ADD [DeniedToolsJson] nvarchar(max) NOT NULL CONSTRAINT DF_NalvaAgents_DeniedTools DEFAULT N'[]';
            IF COL_LENGTH('dbo.NalvaCompanyAgents', 'SensitiveDataRestrictionsJson') IS NULL
                ALTER TABLE [dbo].[NalvaCompanyAgents] ADD [SensitiveDataRestrictionsJson] nvarchar(max) NOT NULL CONSTRAINT DF_NalvaAgents_Sensitive DEFAULT N'[]';
            IF COL_LENGTH('dbo.NalvaCompanyAgents', 'RequireHumanSupervision') IS NULL
                ALTER TABLE [dbo].[NalvaCompanyAgents] ADD [RequireHumanSupervision] bit NOT NULL CONSTRAINT DF_NalvaAgents_Supervision DEFAULT 0;

            IF COL_LENGTH('dbo.TenantSettings', 'RequireMfa') IS NULL
                ALTER TABLE [dbo].[TenantSettings] ADD [RequireMfa] bit NOT NULL CONSTRAINT DF_TenantSettings_RequireMfa DEFAULT 0;
            IF COL_LENGTH('dbo.TenantSettings', 'AuditRetentionDays') IS NULL
                ALTER TABLE [dbo].[TenantSettings] ADD [AuditRetentionDays] int NOT NULL CONSTRAINT DF_TenantSettings_AuditRetention DEFAULT 365;
            IF COL_LENGTH('dbo.TenantSettings', 'SessionIdleMinutes') IS NULL
                ALTER TABLE [dbo].[TenantSettings] ADD [SessionIdleMinutes] int NOT NULL CONSTRAINT DF_TenantSettings_SessionIdle DEFAULT 60;
            IF COL_LENGTH('dbo.TenantSettings', 'MaxConcurrentSessions') IS NULL
                ALTER TABLE [dbo].[TenantSettings] ADD [MaxConcurrentSessions] int NOT NULL CONSTRAINT DF_TenantSettings_MaxSessions DEFAULT 10;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.CompanyBranches', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CompanyBranches](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [Code] nvarchar(50) NULL,
                    [Region] nvarchar(100) NULL,
                    [IsHeadquarters] bit NOT NULL,
                    [ManagerEmployeeId] uniqueidentifier NULL,
                    [Address] nvarchar(500) NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_CompanyBranches] PRIMARY KEY ([Id]));
                CREATE UNIQUE INDEX [IX_CompanyBranches_TenantId_Code] ON [dbo].[CompanyBranches]([TenantId],[Code]) WHERE [Code] IS NOT NULL;
                CREATE INDEX [IX_CompanyBranches_TenantId_IsHeadquarters] ON [dbo].[CompanyBranches]([TenantId],[IsHeadquarters]);
            END

            IF OBJECT_ID(N'dbo.PermissionGroups', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[PermissionGroups](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(150) NOT NULL,
                    [Description] nvarchar(500) NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_PermissionGroups] PRIMARY KEY ([Id]));
                CREATE UNIQUE INDEX [IX_PermissionGroups_TenantId_Name] ON [dbo].[PermissionGroups]([TenantId],[Name]);
            END

            IF OBJECT_ID(N'dbo.PermissionGroupPermissions', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[PermissionGroupPermissions](
                    [PermissionGroupId] uniqueidentifier NOT NULL,
                    [PermissionId] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    CONSTRAINT [PK_PermissionGroupPermissions] PRIMARY KEY ([PermissionGroupId],[PermissionId]),
                    CONSTRAINT [FK_PermissionGroupPermissions_Groups] FOREIGN KEY ([PermissionGroupId]) REFERENCES [dbo].[PermissionGroups]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_PermissionGroupPermissions_Permissions] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permissions]([Id]) ON DELETE NO ACTION);
            END

            IF OBJECT_ID(N'dbo.TemporaryPermissionGrants', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[TemporaryPermissionGrants](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [UserId] nvarchar(450) NOT NULL,
                    [PermissionCode] nvarchar(150) NOT NULL,
                    [RoleId] uniqueidentifier NULL,
                    [ExpiresAt] datetime2 NOT NULL,
                    [GrantedByUserId] nvarchar(450) NOT NULL,
                    [Reason] nvarchar(500) NULL,
                    [IsRevoked] bit NOT NULL,
                    [RevokedAt] datetime2 NULL,
                    [RevokedByUserId] nvarchar(450) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_TemporaryPermissionGrants] PRIMARY KEY ([Id]));
                CREATE INDEX [IX_TemporaryPermissionGrants_Tenant_User_Expires] ON [dbo].[TemporaryPermissionGrants]([TenantId],[UserId],[ExpiresAt]);
            END

            IF OBJECT_ID(N'dbo.RoleAssignmentHistories', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[RoleAssignmentHistories](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [UserId] nvarchar(450) NOT NULL,
                    [RoleId] uniqueidentifier NULL,
                    [RoleName] nvarchar(100) NULL,
                    [PermissionCode] nvarchar(150) NULL,
                    [Action] nvarchar(50) NOT NULL,
                    [ActorUserId] nvarchar(450) NOT NULL,
                    [Reason] nvarchar(500) NULL,
                    [ExpiresAt] datetime2 NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_RoleAssignmentHistories] PRIMARY KEY ([Id]));
                CREATE INDEX [IX_RoleAssignmentHistories_Tenant_User_Created] ON [dbo].[RoleAssignmentHistories]([TenantId],[UserId],[CreatedAt]);
            END

            IF OBJECT_ID(N'dbo.ApprovalPolicies', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ApprovalPolicies](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [Description] nvarchar(1000) NULL,
                    [TriggerType] nvarchar(80) NOT NULL,
                    [AmountThreshold] decimal(18,2) NULL,
                    [Mode] nvarchar(40) NOT NULL,
                    [DeadlineHours] int NOT NULL,
                    [EscalateToUserId] nvarchar(450) NULL,
                    [EscalateToRoleName] nvarchar(100) NULL,
                    [IsActive] bit NOT NULL,
                    [BranchId] uniqueidentifier NULL,
                    [DepartmentId] uniqueidentifier NULL,
                    [ToolName] nvarchar(150) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_ApprovalPolicies] PRIMARY KEY ([Id]));
                CREATE INDEX [IX_ApprovalPolicies_Tenant_Trigger] ON [dbo].[ApprovalPolicies]([TenantId],[TriggerType],[IsActive]);
            END

            IF OBJECT_ID(N'dbo.ApprovalPolicySteps', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ApprovalPolicySteps](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [ApprovalPolicyId] uniqueidentifier NOT NULL,
                    [StepOrder] int NOT NULL,
                    [ApproverType] nvarchar(40) NOT NULL,
                    [ApproverRoleName] nvarchar(100) NULL,
                    [ApproverPermissionCode] nvarchar(150) NULL,
                    [ApproverUserId] nvarchar(450) NULL,
                    [RequireDepartmentHead] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_ApprovalPolicySteps] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ApprovalPolicySteps_Policies] FOREIGN KEY ([ApprovalPolicyId]) REFERENCES [dbo].[ApprovalPolicies]([Id]) ON DELETE CASCADE);
                CREATE UNIQUE INDEX [IX_ApprovalPolicySteps_Policy_Order] ON [dbo].[ApprovalPolicySteps]([ApprovalPolicyId],[StepOrder]);
            END

            IF OBJECT_ID(N'dbo.ApprovalRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ApprovalRequests](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [ApprovalPolicyId] uniqueidentifier NULL,
                    [TriggerType] nvarchar(80) NOT NULL,
                    [SubjectType] nvarchar(100) NOT NULL,
                    [SubjectId] nvarchar(100) NOT NULL,
                    [Title] nvarchar(300) NOT NULL,
                    [PayloadJson] nvarchar(max) NULL,
                    [Amount] decimal(18,2) NULL,
                    [Status] nvarchar(40) NOT NULL,
                    [Mode] nvarchar(40) NOT NULL,
                    [CurrentStepOrder] int NOT NULL,
                    [RequestedAt] datetime2 NOT NULL,
                    [RequestedByUserId] nvarchar(450) NOT NULL,
                    [DeadlineAt] datetime2 NULL,
                    [EscalatedAt] datetime2 NULL,
                    [EscalatedToUserId] nvarchar(450) NULL,
                    [BranchId] uniqueidentifier NULL,
                    [DepartmentId] uniqueidentifier NULL,
                    [NalvaApprovalId] uniqueidentifier NULL,
                    [RejectionReason] nvarchar(1000) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_ApprovalRequests] PRIMARY KEY ([Id]));
                CREATE INDEX [IX_ApprovalRequests_Tenant_Status_Deadline] ON [dbo].[ApprovalRequests]([TenantId],[Status],[DeadlineAt]);
            END

            IF OBJECT_ID(N'dbo.ApprovalDecisions', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ApprovalDecisions](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [ApprovalRequestId] uniqueidentifier NOT NULL,
                    [StepOrder] int NOT NULL,
                    [Decision] nvarchar(40) NOT NULL,
                    [DecidedByUserId] nvarchar(450) NOT NULL,
                    [DelegatedFromUserId] nvarchar(450) NULL,
                    [Note] nvarchar(1000) NULL,
                    [DecidedAt] datetime2 NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_ApprovalDecisions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ApprovalDecisions_Requests] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [dbo].[ApprovalRequests]([Id]) ON DELETE CASCADE);
                CREATE INDEX [IX_ApprovalDecisions_Request_Step] ON [dbo].[ApprovalDecisions]([ApprovalRequestId],[StepOrder]);
            END

            IF OBJECT_ID(N'dbo.EnterpriseAuditEvents', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[EnterpriseAuditEvents](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [Category] nvarchar(80) NOT NULL,
                    [Action] nvarchar(120) NOT NULL,
                    [ActorUserId] nvarchar(450) NULL,
                    [ActorType] nvarchar(40) NULL,
                    [EntityType] nvarchar(120) NULL,
                    [EntityId] nvarchar(100) NULL,
                    [BranchId] nvarchar(64) NULL,
                    [DepartmentId] nvarchar(64) NULL,
                    [IpAddress] nvarchar(64) NULL,
                    [DetailsJson] nvarchar(max) NULL,
                    [IsSensitive] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_EnterpriseAuditEvents] PRIMARY KEY ([Id]));
                CREATE INDEX [IX_EnterpriseAuditEvents_Tenant_Category_Created] ON [dbo].[EnterpriseAuditEvents]([TenantId],[Category],[CreatedAt]);
                CREATE INDEX [IX_EnterpriseAuditEvents_Tenant_Actor_Created] ON [dbo].[EnterpriseAuditEvents]([TenantId],[ActorUserId],[CreatedAt]);
            END

            IF OBJECT_ID(N'dbo.TenantSsoConfigurations', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[TenantSsoConfigurations](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [Protocol] nvarchar(20) NOT NULL,
                    [Provider] nvarchar(80) NOT NULL,
                    [IsEnabled] bit NOT NULL,
                    [DisplayName] nvarchar(200) NULL,
                    [Authority] nvarchar(500) NULL,
                    [ClientId] nvarchar(200) NULL,
                    [ClientSecretProtected] nvarchar(4000) NULL,
                    [AllowedEmailDomains] nvarchar(1000) NULL,
                    [MetadataUrl] nvarchar(1000) NULL,
                    [IdpEntityId] nvarchar(500) NULL,
                    [IdpMetadataXml] nvarchar(max) NULL,
                    [SpEntityId] nvarchar(500) NULL,
                    [DefaultRoleName] nvarchar(100) NULL,
                    [GroupRoleMapJson] nvarchar(max) NULL,
                    [JitProvision] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_TenantSsoConfigurations] PRIMARY KEY ([Id]));
                CREATE INDEX [IX_TenantSsoConfigurations_Tenant_Provider] ON [dbo].[TenantSsoConfigurations]([TenantId],[Provider],[Protocol]);
            END

            IF OBJECT_ID(N'dbo.TenantEnterpriseSecuritySettings', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[TenantEnterpriseSecuritySettings](
                    [Id] uniqueidentifier NOT NULL,
                    [TenantId] uniqueidentifier NOT NULL,
                    [RequireMfa] bit NOT NULL,
                    [SessionIdleMinutes] int NOT NULL,
                    [MaxConcurrentSessions] int NOT NULL,
                    [AuditRetentionDays] int NOT NULL,
                    [EnforceOrgScopeOnQueries] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    [IsDeleted] bit NOT NULL,
                    CONSTRAINT [PK_TenantEnterpriseSecuritySettings] PRIMARY KEY ([Id]));
                CREATE UNIQUE INDEX [IX_TenantEnterpriseSecuritySettings_TenantId] ON [dbo].[TenantEnterpriseSecuritySettings]([TenantId]);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS [dbo].[ApprovalDecisions];
            DROP TABLE IF EXISTS [dbo].[ApprovalRequests];
            DROP TABLE IF EXISTS [dbo].[ApprovalPolicySteps];
            DROP TABLE IF EXISTS [dbo].[ApprovalPolicies];
            DROP TABLE IF EXISTS [dbo].[PermissionGroupPermissions];
            DROP TABLE IF EXISTS [dbo].[PermissionGroups];
            DROP TABLE IF EXISTS [dbo].[TemporaryPermissionGrants];
            DROP TABLE IF EXISTS [dbo].[RoleAssignmentHistories];
            DROP TABLE IF EXISTS [dbo].[EnterpriseAuditEvents];
            DROP TABLE IF EXISTS [dbo].[TenantSsoConfigurations];
            DROP TABLE IF EXISTS [dbo].[TenantEnterpriseSecuritySettings];
            DROP TABLE IF EXISTS [dbo].[CompanyBranches];
            """);
    }
}

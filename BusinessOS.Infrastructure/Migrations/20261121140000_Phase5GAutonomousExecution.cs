using System;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

/// <summary>Phase 5G schedule enrichment, approval policy columns, and MetricsJson SQL Server fix.</summary>
[DbContext(typeof(BusinessOSDbContext))]
[Migration("20261121140000_Phase5GAutonomousExecution")]
public partial class Phase5GAutonomousExecution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('NalvaCompanySettings', 'ApprovalExpiryDays') IS NULL
                ALTER TABLE [NalvaCompanySettings] ADD [ApprovalExpiryDays] int NOT NULL CONSTRAINT DF_NalvaCompanySettings_ApprovalExpiryDays DEFAULT 7;
            IF COL_LENGTH('NalvaCompanySettings', 'AutoExpireApprovals') IS NULL
                ALTER TABLE [NalvaCompanySettings] ADD [AutoExpireApprovals] bit NOT NULL CONSTRAINT DF_NalvaCompanySettings_AutoExpireApprovals DEFAULT 1;
            IF COL_LENGTH('NalvaCompanySettings', 'NotifyPendingApprovals') IS NULL
                ALTER TABLE [NalvaCompanySettings] ADD [NotifyPendingApprovals] bit NOT NULL CONSTRAINT DF_NalvaCompanySettings_NotifyPendingApprovals DEFAULT 1;
            IF COL_LENGTH('NalvaCompanySettings', 'RequireApprovalForExternalActions') IS NULL
                ALTER TABLE [NalvaCompanySettings] ADD [RequireApprovalForExternalActions] bit NOT NULL CONSTRAINT DF_NalvaCompanySettings_RequireApprovalForExternalActions DEFAULT 1;
            IF COL_LENGTH('NalvaCompanySettings', 'ExtraApprovalToolsJson') IS NULL
                ALTER TABLE [NalvaCompanySettings] ADD [ExtraApprovalToolsJson] nvarchar(4000) NOT NULL CONSTRAINT DF_NalvaCompanySettings_ExtraApprovalToolsJson DEFAULT N'[]';

            IF COL_LENGTH('NalvaWorkflowDefinitions', 'RecurrenceKind') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [RecurrenceKind] nvarchar(32) NOT NULL CONSTRAINT DF_NalvaWorkflowDefinitions_RecurrenceKind DEFAULT N'Interval';
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'LocalTimeOfDay') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [LocalTimeOfDay] nvarchar(8) NULL;
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'DaysOfWeek') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [DaysOfWeek] nvarchar(64) NULL;
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'DayOfMonth') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [DayOfMonth] int NULL;
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'IsPaused') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [IsPaused] bit NOT NULL CONSTRAINT DF_NalvaWorkflowDefinitions_IsPaused DEFAULT 0;
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'MaxConcurrentRuns') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [MaxConcurrentRuns] int NOT NULL CONSTRAINT DF_NalvaWorkflowDefinitions_MaxConcurrentRuns DEFAULT 1;
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'ClaimToken') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [ClaimToken] uniqueidentifier NULL;
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'ConsecutiveFailures') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [ConsecutiveFailures] int NOT NULL CONSTRAINT DF_NalvaWorkflowDefinitions_ConsecutiveFailures DEFAULT 0;
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'LastFailure') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [LastFailure] nvarchar(2000) NULL;
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'LastRunAt') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [LastRunAt] datetime2 NULL;
            IF COL_LENGTH('NalvaWorkflowDefinitions', 'CancelledAt') IS NULL
                ALTER TABLE [NalvaWorkflowDefinitions] ADD [CancelledAt] datetime2 NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive_IsPaused_NextRunAt' AND object_id = OBJECT_ID('NalvaWorkflowDefinitions'))
                CREATE INDEX [IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive_IsPaused_NextRunAt]
                ON [NalvaWorkflowDefinitions] ([TenantId], [TriggerKind], [IsActive], [IsPaused], [NextRunAt]);
            """);

        // Clear HasMaxLength(8000) annotation side-effect for SQL Server parameters.
        migrationBuilder.Sql("""
            IF COL_LENGTH('MarketingAnalyticsSnapshots', 'MetricsJson') IS NOT NULL
            BEGIN
                -- Column remains nvarchar(max); annotation-only fix is model-side.
                SELECT 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive_IsPaused_NextRunAt' AND object_id = OBJECT_ID('NalvaWorkflowDefinitions'))
                DROP INDEX [IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive_IsPaused_NextRunAt] ON [NalvaWorkflowDefinitions];
            """);
    }
}

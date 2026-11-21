using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

/// <inheritdoc />
[Migration("20261121143000_Phase6AIntelligentScheduling")]
public partial class Phase6AIntelligentScheduling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ApprovalExpiryDays",
            table: "NalvaCompanySettings",
            type: "int",
            nullable: false,
            defaultValue: 7);

        migrationBuilder.AddColumn<bool>(
            name: "AutoExpireApprovals",
            table: "NalvaCompanySettings",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "ExtraApprovalToolsJson",
            table: "NalvaCompanySettings",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<bool>(
            name: "NotifyPendingApprovals",
            table: "NalvaCompanySettings",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "RequireApprovalForExternalActions",
            table: "NalvaCompanySettings",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "CancelledAt",
            table: "NalvaWorkflowDefinitions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ClaimToken",
            table: "NalvaWorkflowDefinitions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ConsecutiveFailures",
            table: "NalvaWorkflowDefinitions",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "DayOfMonth",
            table: "NalvaWorkflowDefinitions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DaysOfWeek",
            table: "NalvaWorkflowDefinitions",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsPaused",
            table: "NalvaWorkflowDefinitions",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "LastFailure",
            table: "NalvaWorkflowDefinitions",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastRunAt",
            table: "NalvaWorkflowDefinitions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LocalTimeOfDay",
            table: "NalvaWorkflowDefinitions",
            type: "nvarchar(8)",
            maxLength: 8,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MaxConcurrentRuns",
            table: "NalvaWorkflowDefinitions",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<string>(
            name: "RecurrenceKind",
            table: "NalvaWorkflowDefinitions",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Interval");

        migrationBuilder.DropIndex(
            name: "IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive",
            table: "NalvaWorkflowDefinitions");

        migrationBuilder.CreateIndex(
            name: "IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive_IsPaused_NextRunAt",
            table: "NalvaWorkflowDefinitions",
            columns: new[] { "TenantId", "TriggerKind", "IsActive", "IsPaused", "NextRunAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive_IsPaused_NextRunAt",
            table: "NalvaWorkflowDefinitions");

        migrationBuilder.CreateIndex(
            name: "IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive",
            table: "NalvaWorkflowDefinitions",
            columns: new[] { "TenantId", "TriggerKind", "IsActive" });

        migrationBuilder.DropColumn(name: "ApprovalExpiryDays", table: "NalvaCompanySettings");
        migrationBuilder.DropColumn(name: "AutoExpireApprovals", table: "NalvaCompanySettings");
        migrationBuilder.DropColumn(name: "ExtraApprovalToolsJson", table: "NalvaCompanySettings");
        migrationBuilder.DropColumn(name: "NotifyPendingApprovals", table: "NalvaCompanySettings");
        migrationBuilder.DropColumn(name: "RequireApprovalForExternalActions", table: "NalvaCompanySettings");

        migrationBuilder.DropColumn(name: "CancelledAt", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "ClaimToken", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "ConsecutiveFailures", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "DayOfMonth", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "DaysOfWeek", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "IsPaused", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "LastFailure", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "LastRunAt", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "LocalTimeOfDay", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "MaxConcurrentRuns", table: "NalvaWorkflowDefinitions");
        migrationBuilder.DropColumn(name: "RecurrenceKind", table: "NalvaWorkflowDefinitions");
    }
}

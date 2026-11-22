using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Phase8D_AutonomousOperations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BusinessAutopilotConfigs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Mode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                EmergencyStop = table.Column<bool>(type: "bit", nullable: false),
                ObjectivesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PrioritiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                MonthlySalesTarget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                MarketingBudgetMonthly = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                SpendingLimitPerAction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                DailySpendingLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                InventoryPolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CustomerServicePolicyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ApprovalRequirementsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                WorkingHoursJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                AgentResponsibilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PausedAgentKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                EnabledScenarioKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ControlledAllowlistToolsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                AllowedMediumToolsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                TimeZoneId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LastActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastActivatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BusinessAutopilotConfigs", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_BusinessAutopilotConfigs_TenantId",
            table: "BusinessAutopilotConfigs",
            column: "TenantId",
            unique: true);

        migrationBuilder.CreateTable(
            name: "BusinessGoals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                MetricKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                TargetValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                BaselineValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                PredictedValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                ActualValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                Unit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                AssignedAgentKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                RecommendedActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CompletedActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                OutcomeNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                LastMeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                EvidenceLinked = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BusinessGoals", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_BusinessGoals_TenantId_Status_PeriodEndUtc",
            table: "BusinessGoals",
            columns: new[] { "TenantId", "Status", "PeriodEndUtc" });

        migrationBuilder.CreateTable(
            name: "BusinessGoalProgress",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                PredictedValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                ActualValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                MeasuredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BusinessGoalProgress", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_BusinessGoalProgress_TenantId_GoalId_MeasuredAtUtc",
            table: "BusinessGoalProgress",
            columns: new[] { "TenantId", "GoalId", "MeasuredAtUtc" });

        migrationBuilder.CreateTable(
            name: "BusinessMemoryEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SourceAttribution = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                SourceEntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                VectorPointId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsTrusted = table.Column<bool>(type: "bit", nullable: false),
                CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BusinessMemoryEntries", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_BusinessMemoryEntries_TenantId_Kind_CreatedAt",
            table: "BusinessMemoryEntries",
            columns: new[] { "TenantId", "Kind", "CreatedAt" });

        migrationBuilder.CreateTable(
            name: "AutopilotOperationLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ScenarioKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                ToolName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                WorkflowRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ExternalEffect = table.Column<bool>(type: "bit", nullable: false),
                FinancialEffect = table.Column<bool>(type: "bit", nullable: false),
                EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AutopilotOperationLogs", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AutopilotOperationLogs_TenantId_CreatedAt",
            table: "AutopilotOperationLogs",
            columns: new[] { "TenantId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AutopilotOperationLogs_TenantId_Kind_Status",
            table: "AutopilotOperationLogs",
            columns: new[] { "TenantId", "Kind", "Status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AutopilotOperationLogs");
        migrationBuilder.DropTable(name: "BusinessMemoryEntries");
        migrationBuilder.DropTable(name: "BusinessGoalProgress");
        migrationBuilder.DropTable(name: "BusinessGoals");
        migrationBuilder.DropTable(name: "BusinessAutopilotConfigs");
    }
}

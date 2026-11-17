using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelAfterPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NalvaAgentRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaAgentRevisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ToolName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Risk = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArgsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApproverUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutionResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaCompanyAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DefinitionVersion = table.Column<int>(type: "int", nullable: false),
                    IsCustom = table.Column<bool>(type: "bit", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RoleTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AutonomyLevel = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Temperature = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    DailyExecutionLimit = table.Column<int>(type: "int", nullable: false),
                    MonthlyTokenBudget = table.Column<int>(type: "int", nullable: false),
                    ToolsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PermissionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponsibilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovalActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HealthMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaCompanyAgents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaCompanySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultModel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EmbeddingModel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    MonthlyTokenBudget = table.Column<int>(type: "int", nullable: false),
                    MonthlyBudgetUsd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DailyExecutionLimit = table.Column<int>(type: "int", nullable: false),
                    AlertThresholdPercent = table.Column<int>(type: "int", nullable: false),
                    CostAlert = table.Column<bool>(type: "bit", nullable: false),
                    AutonomyCeiling = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaCompanySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DelegationChain = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Depth = table.Column<int>(type: "int", nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CostIsEstimate = table.Column<bool>(type: "bit", nullable: false),
                    PricingVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToolCallCount = table.Column<int>(type: "int", nullable: false),
                    ToolLogJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovalCount = table.Column<int>(type: "int", nullable: false),
                    RetryAttempt = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    InitiatorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancelRequested = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaKnowledgeSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaKnowledgeSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaMemoryRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaMemoryRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RunAfter = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ParentTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Depth = table.Column<int>(type: "int", nullable: false),
                    DelegationChain = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InitiatorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ToolPlanJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArgumentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorClass = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaWorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TriggerKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TriggerEvent = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    StepsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxToolCalls = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaWorkflowDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaWorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentStep = table.Column<int>(type: "int", nullable: false),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaWorkflowRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaWorkItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaAgentRevisions_TenantId_CompanyAgentId_Version",
                table: "NalvaAgentRevisions",
                columns: new[] { "TenantId", "CompanyAgentId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaApprovals_TenantId_Status_RequestedAt",
                table: "NalvaApprovals",
                columns: new[] { "TenantId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaCompanyAgents_TenantId_AgentKey",
                table: "NalvaCompanyAgents",
                columns: new[] { "TenantId", "AgentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NalvaCompanySettings_TenantId",
                table: "NalvaCompanySettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NalvaExecutions_TenantId_AgentKey_StartedAt",
                table: "NalvaExecutions",
                columns: new[] { "TenantId", "AgentKey", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaKnowledgeSources_TenantId_AgentKey",
                table: "NalvaKnowledgeSources",
                columns: new[] { "TenantId", "AgentKey" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaMemoryRecords_TenantId_AgentKey_Kind",
                table: "NalvaMemoryRecords",
                columns: new[] { "TenantId", "AgentKey", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaTasks_TenantId_Status_RunAfter",
                table: "NalvaTasks",
                columns: new[] { "TenantId", "Status", "RunAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaWorkflowDefinitions_TenantId_Key",
                table: "NalvaWorkflowDefinitions",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NalvaWorkflowRuns_TenantId_DefinitionId_StartedAt",
                table: "NalvaWorkflowRuns",
                columns: new[] { "TenantId", "DefinitionId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaWorkItems_TenantId_AgentKey_Kind",
                table: "NalvaWorkItems",
                columns: new[] { "TenantId", "AgentKey", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NalvaAgentRevisions");

            migrationBuilder.DropTable(
                name: "NalvaApprovals");

            migrationBuilder.DropTable(
                name: "NalvaCompanyAgents");

            migrationBuilder.DropTable(
                name: "NalvaCompanySettings");

            migrationBuilder.DropTable(
                name: "NalvaExecutions");

            migrationBuilder.DropTable(
                name: "NalvaKnowledgeSources");

            migrationBuilder.DropTable(
                name: "NalvaMemoryRecords");

            migrationBuilder.DropTable(
                name: "NalvaTasks");

            migrationBuilder.DropTable(
                name: "NalvaWorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "NalvaWorkflowRuns");

            migrationBuilder.DropTable(
                name: "NalvaWorkItems");
        }
    }
}

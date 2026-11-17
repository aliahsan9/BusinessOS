using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNalvaAutonomyAndIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "NalvaWorkflowRuns",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefinitionVersion",
                table: "NalvaWorkflowRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "NalvaWorkflowRuns",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsDryRun",
                table: "NalvaWorkflowRuns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StepLogJson",
                table: "NalvaWorkflowRuns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StepsSnapshotJson",
                table: "NalvaWorkflowRuns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TriggerSource",
                table: "NalvaWorkflowRuns",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastScheduleWindow",
                table: "NalvaWorkflowDefinitions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRunAt",
                table: "NalvaWorkflowDefinitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduleEndUtc",
                table: "NalvaWorkflowDefinitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduleStartUtc",
                table: "NalvaWorkflowDefinitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "NalvaWorkflowDefinitions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "NalvaWorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "NalvaExecutions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModelCallCount",
                table: "NalvaExecutions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "NalvaApprovals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyIntegrationConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ScopesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccessTokenProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefreshTokenProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalAccountLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WebhookSecretProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastSuccessAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyIntegrationConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    ErrorRedacted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationOAuthStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    State = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CodeVerifier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RedirectUri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOAuthStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NalvaBusinessEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WorkflowsStarted = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NalvaBusinessEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaWorkflowRuns_TenantId_CorrelationId",
                table: "NalvaWorkflowRuns",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive",
                table: "NalvaWorkflowDefinitions",
                columns: new[] { "TenantId", "TriggerKind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyIntegrationConnections_TenantId_ProviderKey",
                table: "CompanyIntegrationConnections",
                columns: new[] { "TenantId", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationActivityLogs_TenantId_ProviderKey_CreatedAt",
                table: "IntegrationActivityLogs",
                columns: new[] { "TenantId", "ProviderKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOAuthStates_TenantId_State",
                table: "IntegrationOAuthStates",
                columns: new[] { "TenantId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaBusinessEvents_TenantId_IdempotencyKey",
                table: "NalvaBusinessEvents",
                columns: new[] { "TenantId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_NalvaBusinessEvents_TenantId_Status_OccurredAt",
                table: "NalvaBusinessEvents",
                columns: new[] { "TenantId", "Status", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyIntegrationConnections");

            migrationBuilder.DropTable(
                name: "IntegrationActivityLogs");

            migrationBuilder.DropTable(
                name: "IntegrationOAuthStates");

            migrationBuilder.DropTable(
                name: "NalvaBusinessEvents");

            migrationBuilder.DropIndex(
                name: "IX_NalvaWorkflowRuns_TenantId_CorrelationId",
                table: "NalvaWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_NalvaWorkflowDefinitions_TenantId_TriggerKind_IsActive",
                table: "NalvaWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "NalvaWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "DefinitionVersion",
                table: "NalvaWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "EstimatedCostUsd",
                table: "NalvaWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "IsDryRun",
                table: "NalvaWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "StepLogJson",
                table: "NalvaWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "StepsSnapshotJson",
                table: "NalvaWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TriggerSource",
                table: "NalvaWorkflowRuns");

            migrationBuilder.DropColumn(
                name: "LastScheduleWindow",
                table: "NalvaWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "NextRunAt",
                table: "NalvaWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ScheduleEndUtc",
                table: "NalvaWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ScheduleStartUtc",
                table: "NalvaWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "NalvaWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "NalvaWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "NalvaExecutions");

            migrationBuilder.DropColumn(
                name: "ModelCallCount",
                table: "NalvaExecutions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "NalvaApprovals");
        }
    }
}

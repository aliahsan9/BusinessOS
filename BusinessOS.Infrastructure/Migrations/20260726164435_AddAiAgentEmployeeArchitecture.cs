using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAgentEmployeeArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoleTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Specialty = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SystemPersonaPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgentKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentStepIndex = table.Column<int>(type: "int", nullable: false),
                    ProgressJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflowRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoicePreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false),
                    VoiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SpeechRate = table.Column<double>(type: "float", nullable: false),
                    Pitch = table.Column<double>(type: "float", nullable: false),
                    ContinuousListening = table.Column<bool>(type: "bit", nullable: false),
                    AutoSpeak = table.Column<bool>(type: "bit", nullable: false),
                    PreferredAgentKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoicePreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentWorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentWorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentWorkflowSteps_AgentWorkflowRuns_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "AgentWorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentProfiles_TenantId_IsDefault_IsActive",
                table: "AgentProfiles",
                columns: new[] { "TenantId", "IsDefault", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentProfiles_TenantId_Key",
                table: "AgentProfiles",
                columns: new[] { "TenantId", "Key" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowRuns_TenantId_Status_StartedAt",
                table: "AgentWorkflowRuns",
                columns: new[] { "TenantId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowRuns_TenantId_UserId_StartedAt",
                table: "AgentWorkflowRuns",
                columns: new[] { "TenantId", "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowSteps_WorkflowRunId_SortOrder",
                table: "AgentWorkflowSteps",
                columns: new[] { "WorkflowRunId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowSteps_WorkflowRunId_StepKey",
                table: "AgentWorkflowSteps",
                columns: new[] { "WorkflowRunId", "StepKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoicePreferences_TenantId_UserId",
                table: "VoicePreferences",
                columns: new[] { "TenantId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentProfiles");

            migrationBuilder.DropTable(
                name: "AgentWorkflowSteps");

            migrationBuilder.DropTable(
                name: "VoicePreferences");

            migrationBuilder.DropTable(
                name: "AgentWorkflowRuns");
        }
    }
}

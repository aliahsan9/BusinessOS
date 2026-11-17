using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase7AgencyLaunch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgencyWorkspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentStep = table.Column<int>(type: "int", nullable: false),
                    CompanySaved = table.Column<bool>(type: "bit", nullable: false),
                    ProfileSaved = table.Column<bool>(type: "bit", nullable: false),
                    WorkspaceSaved = table.Column<bool>(type: "bit", nullable: false),
                    WorkforceSaved = table.Column<bool>(type: "bit", nullable: false),
                    PlanSaved = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TemplateKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Services = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Technologies = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TeamSize = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TargetClients = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Goals = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ModulesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    InvitedEmailsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RolesJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    InitialProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InitialProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgentsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ApprovalPreference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MonthlyAiBudgetUsd = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    SelectedPlanKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FirstObjective = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FirstTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitesCreated = table.Column<int>(type: "int", nullable: false),
                    InviteNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgencyWorkspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyDataRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDataRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EarlyAccessInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AgencyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RedeemedCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarlyAccessInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LaunchFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    Page = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    BacklogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaunchFeedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductBacklogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FeedbackId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBacklogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductUsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductUsageEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgencyWorkspaces_TenantId",
                table: "AgencyWorkspaces",
                column: "TenantId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDataRequests_TenantId_Kind_Status",
                table: "CompanyDataRequests",
                columns: new[] { "TenantId", "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EarlyAccessInvitations_Code",
                table: "EarlyAccessInvitations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EarlyAccessInvitations_Status",
                table: "EarlyAccessInvitations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LaunchFeedback_TenantId_Kind_CreatedAt",
                table: "LaunchFeedback",
                columns: new[] { "TenantId", "Kind", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBacklogItems_Priority_Status",
                table: "ProductBacklogItems",
                columns: new[] { "Priority", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductUsageEvents_TenantId_EventKey_OccurredAt",
                table: "ProductUsageEvents",
                columns: new[] { "TenantId", "EventKey", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgencyWorkspaces");

            migrationBuilder.DropTable(
                name: "CompanyDataRequests");

            migrationBuilder.DropTable(
                name: "EarlyAccessInvitations");

            migrationBuilder.DropTable(
                name: "LaunchFeedback");

            migrationBuilder.DropTable(
                name: "ProductBacklogItems");

            migrationBuilder.DropTable(
                name: "ProductUsageEvents");
        }
    }
}

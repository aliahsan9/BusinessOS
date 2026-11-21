using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

/// <inheritdoc />
[Migration("20261121114500_Phase5CCustomerSupport")]
public partial class Phase5CCustomerSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SupportChannelAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                ExternalAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AllowsOutbound = table.Column<bool>(type: "bit", nullable: false),
                AllowsInbound = table.Column<bool>(type: "bit", nullable: false),
                LastInboundAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastOutboundAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SupportChannelAccounts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SupportTickets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TicketNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Priority = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                AssignedAgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                AssignedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                ExternalThreadId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                IdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                AiSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                RecommendedAction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Resolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                EscalationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                EscalationSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastCustomerMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastAgentMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RelatedOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                HumanTakeover = table.Column<bool>(type: "bit", nullable: false),
                MarketingConsentForHandoff = table.Column<bool>(type: "bit", nullable: false),
                ReopenCount = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SupportTickets", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SupportFollowUpJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                DueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                MessageTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                DedupKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                RespectBusinessHours = table.Column<bool>(type: "bit", nullable: false),
                OptOutHonored = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportFollowUpJobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupportFollowUpJobs_SupportTickets_TicketId",
                    column: x => x.TicketId,
                    principalTable: "SupportTickets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SupportMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                AuthorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                AuthorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                DeliveryStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ExternalMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                IdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                IsInternalNote = table.Column<bool>(type: "bit", nullable: false),
                Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_SupportMessages_SupportTickets_TicketId",
                    column: x => x.TicketId,
                    principalTable: "SupportTickets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SupportChannelAccounts_TenantId_Channel",
            table: "SupportChannelAccounts",
            columns: new[] { "TenantId", "Channel" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SupportFollowUpJobs_TenantId_DedupKey",
            table: "SupportFollowUpJobs",
            columns: new[] { "TenantId", "DedupKey" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportFollowUpJobs_TenantId_Status_DueAt",
            table: "SupportFollowUpJobs",
            columns: new[] { "TenantId", "Status", "DueAt" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportFollowUpJobs_TicketId",
            table: "SupportFollowUpJobs",
            column: "TicketId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportMessages_TenantId_ExternalMessageId",
            table: "SupportMessages",
            columns: new[] { "TenantId", "ExternalMessageId" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportMessages_TenantId_IdempotencyKey",
            table: "SupportMessages",
            columns: new[] { "TenantId", "IdempotencyKey" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportMessages_TenantId_TicketId_CreatedAt",
            table: "SupportMessages",
            columns: new[] { "TenantId", "TicketId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportMessages_TicketId",
            table: "SupportMessages",
            column: "TicketId");

        migrationBuilder.CreateIndex(
            name: "IX_SupportTickets_TenantId_ExternalThreadId",
            table: "SupportTickets",
            columns: new[] { "TenantId", "ExternalThreadId" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportTickets_TenantId_IdempotencyKey",
            table: "SupportTickets",
            columns: new[] { "TenantId", "IdempotencyKey" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportTickets_TenantId_Status_Priority",
            table: "SupportTickets",
            columns: new[] { "TenantId", "Status", "Priority" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportTickets_TenantId_TicketNumber",
            table: "SupportTickets",
            columns: new[] { "TenantId", "TicketNumber" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SupportChannelAccounts");
        migrationBuilder.DropTable(name: "SupportFollowUpJobs");
        migrationBuilder.DropTable(name: "SupportMessages");
        migrationBuilder.DropTable(name: "SupportTickets");
    }
}

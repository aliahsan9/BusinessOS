using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

/// <inheritdoc />
public class Phase6BExternalIntegrations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CompanyPaymentRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExternalPaymentId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ExternalCustomerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                AmountMinor = table.Column<long>(type: "bigint", nullable: true),
                Currency = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CompanyPaymentRecords", x => x.Id));

        migrationBuilder.CreateTable(
            name: "IntegrationConversations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ExternalThreadId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CustomerExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                CustomerDisplay = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                LastInboundAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastOutboundAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CustomerCareWindowEndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_IntegrationConversations", x => x.Id));

        migrationBuilder.CreateTable(
            name: "IntegrationConversationMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Direction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                MessageType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ExternalMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                TemplateName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                Approved = table.Column<bool>(type: "bit", nullable: false),
                DeliveryStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_IntegrationConversationMessages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "IntegrationDeadLetters",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ProviderKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Operation = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                EventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ErrorRedacted = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                PayloadRedacted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Attempts = table.Column<int>(type: "int", nullable: false),
                NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_IntegrationDeadLetters", x => x.Id));

        migrationBuilder.CreateTable(
            name: "IntegrationWebhookReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                EventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ErrorRedacted = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_IntegrationWebhookReceipts", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_CompanyPaymentRecords_TenantId_IdempotencyKey",
            table: "CompanyPaymentRecords",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true,
            filter: "[IdempotencyKey] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_CompanyPaymentRecords_TenantId_ProviderKey_ExternalPaymentId",
            table: "CompanyPaymentRecords",
            columns: new[] { "TenantId", "ProviderKey", "ExternalPaymentId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationConversationMessages_TenantId_ConversationId_CreatedAt",
            table: "IntegrationConversationMessages",
            columns: new[] { "TenantId", "ConversationId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationConversationMessages_TenantId_IdempotencyKey",
            table: "IntegrationConversationMessages",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true,
            filter: "[IdempotencyKey] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationConversations_TenantId_ProviderKey_ExternalThreadId",
            table: "IntegrationConversations",
            columns: new[] { "TenantId", "ProviderKey", "ExternalThreadId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationDeadLetters_TenantId_Status_CreatedAt",
            table: "IntegrationDeadLetters",
            columns: new[] { "TenantId", "Status", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_IntegrationWebhookReceipts_ProviderKey_EventId",
            table: "IntegrationWebhookReceipts",
            columns: new[] { "ProviderKey", "EventId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CompanyPaymentRecords");
        migrationBuilder.DropTable(name: "IntegrationConversationMessages");
        migrationBuilder.DropTable(name: "IntegrationConversations");
        migrationBuilder.DropTable(name: "IntegrationDeadLetters");
        migrationBuilder.DropTable(name: "IntegrationWebhookReceipts");
    }
}

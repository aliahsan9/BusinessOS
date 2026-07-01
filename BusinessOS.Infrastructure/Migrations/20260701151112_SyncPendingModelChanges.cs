using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AIConversations",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Prompt",
                table: "AIConversations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "CitationsJson",
                table: "AIConversations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutionTimeMs",
                table: "AIConversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Intent",
                table: "AIConversations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "AIConversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenUsage",
                table: "AIConversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToolsUsedJson",
                table: "AIConversations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiConversationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CurrentPage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SelectedCustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemoryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConversationSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiCopilotAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Intent = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ToolsUsedJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetrievedDocumentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutionTimeMs = table.Column<int>(type: "int", nullable: false),
                    TokenUsage = table.Column<int>(type: "int", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCopilotAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsIndexed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiDocumentChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChunkIndex = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiDocumentChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiDocumentChunks_AiDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "AiDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIConversations_SessionId",
                table: "AIConversations",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversations_TenantId_UserId_CreatedAt",
                table: "AIConversations",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversationSessions_TenantId_UserId_LastActivityAt",
                table: "AiConversationSessions",
                columns: new[] { "TenantId", "UserId", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiCopilotAuditLogs_TenantId_UserId_CreatedAt",
                table: "AiCopilotAuditLogs",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDocumentChunks_DocumentId",
                table: "AiDocumentChunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AiDocumentChunks_TenantId_DocumentType",
                table: "AiDocumentChunks",
                columns: new[] { "TenantId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_AiDocuments_TenantId_DocumentType",
                table: "AiDocuments",
                columns: new[] { "TenantId", "DocumentType" });

            migrationBuilder.AddForeignKey(
                name: "FK_AIConversations_AiConversationSessions_SessionId",
                table: "AIConversations",
                column: "SessionId",
                principalTable: "AiConversationSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIConversations_AiConversationSessions_SessionId",
                table: "AIConversations");

            migrationBuilder.DropTable(
                name: "AiConversationSessions");

            migrationBuilder.DropTable(
                name: "AiCopilotAuditLogs");

            migrationBuilder.DropTable(
                name: "AiDocumentChunks");

            migrationBuilder.DropTable(
                name: "AiDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AIConversations_SessionId",
                table: "AIConversations");

            migrationBuilder.DropIndex(
                name: "IX_AIConversations_TenantId_UserId_CreatedAt",
                table: "AIConversations");

            migrationBuilder.DropColumn(
                name: "CitationsJson",
                table: "AIConversations");

            migrationBuilder.DropColumn(
                name: "ExecutionTimeMs",
                table: "AIConversations");

            migrationBuilder.DropColumn(
                name: "Intent",
                table: "AIConversations");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "AIConversations");

            migrationBuilder.DropColumn(
                name: "TokenUsage",
                table: "AIConversations");

            migrationBuilder.DropColumn(
                name: "ToolsUsedJson",
                table: "AIConversations");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "AIConversations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "Prompt",
                table: "AIConversations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);
        }
    }
}

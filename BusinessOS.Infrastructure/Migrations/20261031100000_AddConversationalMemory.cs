using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BusinessOS.Infrastructure.Data;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BusinessOSDbContext))]
    [Migration("20261031100000_AddConversationalMemory")]
    public partial class AddConversationalMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSummarizedAt",
                table: "AiConversationSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSummarizedMessageSequence",
                table: "AiConversationSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "AiConversationSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiConversationSessions_TenantId_UserId_IsActive_LastActivityAt",
                table: "AiConversationSessions",
                columns: new[] { "TenantId", "UserId", "IsActive", "LastActivityAt" });

            migrationBuilder.CreateTable(
                name: "AiMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    TokenCount = table.Column<int>(type: "int", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TurnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiMessages_AiConversationSessions_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AiConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_ConversationId_CreatedAt",
                table: "AiMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_ConversationId_SequenceNumber",
                table: "AiMessages",
                columns: new[] { "ConversationId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiMessages_TenantId_UserId_CreatedAt",
                table: "AiMessages",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AiMessages");

            migrationBuilder.DropIndex(
                name: "IX_AiConversationSessions_TenantId_UserId_IsActive_LastActivityAt",
                table: "AiConversationSessions");

            migrationBuilder.DropColumn(
                name: "LastSummarizedAt",
                table: "AiConversationSessions");

            migrationBuilder.DropColumn(
                name: "LastSummarizedMessageSequence",
                table: "AiConversationSessions");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "AiConversationSessions");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivitiesAndNotificationEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CustomerNotificationsEnabled",
                table: "TenantSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InvoiceNotificationsEnabled",
                table: "TenantSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ProjectNotificationsEnabled",
                table: "TenantSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TaskNotificationsEnabled",
                table: "TenantSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TenantId_CreatedAt",
                table: "Activities",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_TenantId_EntityType_EntityId_Action",
                table: "Activities",
                columns: new[] { "TenantId", "EntityType", "EntityId", "Action" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropColumn(
                name: "CustomerNotificationsEnabled",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "InvoiceNotificationsEnabled",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "ProjectNotificationsEnabled",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "TaskNotificationsEnabled",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Notifications");
        }
    }
}

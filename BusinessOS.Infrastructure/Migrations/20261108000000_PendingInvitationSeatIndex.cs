using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    [DbContext(typeof(BusinessOSDbContext))]
    [Migration("20261108000000_PendingInvitationSeatIndex")]
    public partial class PendingInvitationSeatIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the newest pending invite for each tenant email and cancel older duplicates.
            // Rows are retained. This only releases extra reserved seats so the unique index can be created.
            migrationBuilder.Sql("""
                ;WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY TenantId, Email
                               ORDER BY CreatedAt DESC, Id DESC) AS rn
                    FROM TeamInvitations
                    WHERE Status = 0 AND IsDeleted = 0
                )
                UPDATE TeamInvitations
                SET Status = 3,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id IN (SELECT Id FROM ranked WHERE rn > 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TeamInvitations_OnePendingEmail",
                table: "TeamInvitations",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamInvitations_OnePendingEmail",
                table: "TeamInvitations");
        }
    }
}

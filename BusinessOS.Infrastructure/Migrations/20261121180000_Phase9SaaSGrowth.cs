using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

/// <inheritdoc />
public class Phase9SaaSGrowth : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MarketingLeads",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Topic = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                SourcePath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ReferralCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                MarketingOptIn = table.Column<bool>(type: "bit", nullable: false),
                ConsentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                ConvertedCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_MarketingLeads", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PlatformSupportTickets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                RequesterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequesterEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Message = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Priority = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                AgentReply = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                EscalatedToHuman = table.Column<bool>(type: "bit", nullable: false),
                ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PlatformSupportTickets", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ReferralCodes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                RedemptionCount = table.Column<int>(type: "int", nullable: false),
                MaxRedemptions = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ReferralCodes", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ReferralRedemptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferralCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferrerTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReferredTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReferredEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ConvertedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ReferralRedemptions", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_MarketingLeads_Email", table: "MarketingLeads", column: "Email");
        migrationBuilder.CreateIndex(name: "IX_MarketingLeads_Kind", table: "MarketingLeads", column: "Kind");
        migrationBuilder.CreateIndex(name: "IX_MarketingLeads_Status_CreatedAt", table: "MarketingLeads", columns: new[] { "Status", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_PlatformSupportTickets_RequesterEmail", table: "PlatformSupportTickets", column: "RequesterEmail");
        migrationBuilder.CreateIndex(name: "IX_PlatformSupportTickets_Status_CreatedAt", table: "PlatformSupportTickets", columns: new[] { "Status", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_PlatformSupportTickets_TenantId", table: "PlatformSupportTickets", column: "TenantId");
        migrationBuilder.CreateIndex(name: "IX_ReferralCodes_Code", table: "ReferralCodes", column: "Code", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(name: "IX_ReferralCodes_TenantId", table: "ReferralCodes", column: "TenantId", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex(name: "IX_ReferralRedemptions_ReferralCodeId_CreatedAt", table: "ReferralRedemptions", columns: new[] { "ReferralCodeId", "CreatedAt" });
        migrationBuilder.CreateIndex(name: "IX_ReferralRedemptions_ReferredEmail", table: "ReferralRedemptions", column: "ReferredEmail");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MarketingLeads");
        migrationBuilder.DropTable(name: "PlatformSupportTickets");
        migrationBuilder.DropTable(name: "ReferralCodes");
        migrationBuilder.DropTable(name: "ReferralRedemptions");
    }
}

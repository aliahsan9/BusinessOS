using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

[DbContext(typeof(BusinessOSDbContext))]
[Migration("20261117120000_AddAgentProfileToolPermissions")]
public partial class AddAgentProfileToolPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AllowedToolsJson",
            table: "AgentProfiles",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AllowedToolsJson",
            table: "AgentProfiles");
    }
}

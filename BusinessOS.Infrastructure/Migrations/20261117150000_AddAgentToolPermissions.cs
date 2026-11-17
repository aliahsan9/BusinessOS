using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BusinessOS.Infrastructure.Data;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

[DbContext(typeof(BusinessOSDbContext))]
[Migration("20261117150000_AddAgentToolPermissions")]
public partial class AddAgentToolPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AllowedToolsJson",
            table: "AgentProfiles",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "[]");

        // Existing system and tenant profiles receive a conservative, role-oriented read-only baseline.
        // Unknown keys remain deny-all until explicitly configured by a tenant Owner/Admin.
        migrationBuilder.Sql("""
            UPDATE [AgentProfiles]
            SET [AllowedToolsJson] = CASE LOWER([Key])
                WHEN 'adam' THEN N'["GetProducts","SearchProduct","GetInventorySummary","GetLowStock","GetOutOfStock","GetDeadStock","GetPurchaseRecommendations","GetInventoryHealth","SearchSupplier"]'
                WHEN 'emma' THEN N'["GetCustomers","SearchCustomer","GetInvoices","SearchInvoice","GetRevenue","GetSalesSummary","GetBestSellingProducts","GetSalesTrends","GetCustomerInsights","GetBusinessHealth","GetDailyPriorities","ShowProfit"]'
                WHEN 'sophia' THEN N'["GetCustomers","GetProjects","GetTasks","GetInvoices","GetExpenses","GetProducts","GetRevenue","GetSalesSummary","GetBestSellingProducts","GetSalesTrends","GetInventorySummary","GetLowStock","GetDeadStock","GetPurchaseRecommendations","GetBusinessSettings","GetNotificationsSummary","SearchDocuments","ShowProfit","SearchCustomer","SearchProduct","SearchInvoice","SearchSupplier","GetOutOfStock","GetInventoryHealth","GetBusinessHealth","GetDailyPriorities","GetCustomerInsights"]'
                ELSE N'[]'
            END
            WHERE [AllowedToolsJson] = N'[]';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AllowedToolsJson",
            table: "AgentProfiles");
    }
}

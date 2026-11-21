using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

/// <summary>
/// Adds OrderDate indexes used by finance, analytics, and dashboard queries.
/// Evidence: hot paths filter Orders by OrderDate + Status while indexes were only on CreatedAt.
/// </summary>
[DbContext(typeof(BusinessOSDbContext))]
[Migration("20261121180000_Phase7BOrderDateIndexes")]
public partial class Phase7BOrderDateIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Orders_TenantId_OrderDate' AND object_id = OBJECT_ID(N'dbo.Orders'))
            BEGIN
                CREATE NONCLUSTERED INDEX [IX_Orders_TenantId_OrderDate]
                ON [dbo].[Orders] ([TenantId], [OrderDate]);
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Orders_TenantId_Status_OrderDate' AND object_id = OBJECT_ID(N'dbo.Orders'))
            BEGIN
                CREATE NONCLUSTERED INDEX [IX_Orders_TenantId_Status_OrderDate]
                ON [dbo].[Orders] ([TenantId], [Status], [OrderDate] DESC);
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS [IX_Orders_TenantId_Status_OrderDate] ON [dbo].[Orders];
            DROP INDEX IF EXISTS [IX_Orders_TenantId_OrderDate] ON [dbo].[Orders];
            """);
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyIndexesAndPaymentIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM Inventories WHERE CurrentStock < 0)
                    THROW 50001, 'Cannot add CK_Inventories_CurrentStock_NonNegative because Inventories.CurrentStock contains negative values. Correct those rows before applying this migration. No rows were deleted.', 1;
                IF EXISTS (SELECT 1 FROM Products WHERE CurrentStock < 0)
                    THROW 50001, 'Cannot add CK_Products_CurrentStock_NonNegative because Products.CurrentStock contains negative values. Correct those rows before applying this migration. No rows were deleted.', 1;
                IF EXISTS (
                    SELECT 1
                    FROM BillingTransactions
                    WHERE TransactionId IS NOT NULL
                    GROUP BY Provider, TransactionId
                    HAVING COUNT(*) > 1)
                    THROW 50001, 'Cannot add IX_BillingTransactions_Provider_TransactionId because duplicate provider transaction references exist. Resolve those duplicates before applying this migration. No rows were deleted.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_Status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_BillingTransactions_TransactionId",
                table: "BillingTransactions");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "ProviderOperationKey",
                table: "Payments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "Payments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Payments",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Payments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Completed");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Orders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Inventories",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyRecordId",
                table: "BillingTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderOperationKey",
                table: "BillingTransactions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "BillingTransactions",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedWebhookEvents", x => x.Id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_CurrentStock_NonNegative",
                table: "Products",
                sql: "[CurrentStock] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_CustomerId_PaymentDate",
                table: "Payments",
                columns: new[] { "TenantId", "CustomerId", "PaymentDate" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_OrderId",
                table: "Payments",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_PaymentDate",
                table: "Payments",
                columns: new[] { "TenantId", "PaymentDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId_Status_PaymentDate",
                table: "Payments",
                columns: new[] { "TenantId", "Status", "PaymentDate" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_CustomerId_CreatedAt",
                table: "Orders",
                columns: new[] { "TenantId", "CustomerId", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_Status_CreatedAt",
                table: "Orders",
                columns: new[] { "TenantId", "Status", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventories_CurrentStock_NonNegative",
                table: "Inventories",
                sql: "[CurrentStock] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_City",
                table: "Customers",
                columns: new[] { "TenantId", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingTransactions_IdempotencyRecordId",
                table: "BillingTransactions",
                column: "IdempotencyRecordId",
                unique: true,
                filter: "[IdempotencyRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BillingTransactions_Provider_TransactionId",
                table: "BillingTransactions",
                columns: new[] { "Provider", "TransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingTransactions_TenantId_Status_CreatedAt",
                table: "BillingTransactions",
                columns: new[] { "TenantId", "Status", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_TenantId_ExpiresAt",
                table: "IdempotencyRecords",
                columns: new[] { "TenantId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_TenantId_Operation_Key",
                table: "IdempotencyRecords",
                columns: new[] { "TenantId", "Operation", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedWebhookEvents_Provider_EventId",
                table: "ProcessedWebhookEvents",
                columns: new[] { "Provider", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "ProcessedWebhookEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_CurrentStock_NonNegative",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId_CustomerId_PaymentDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId_OrderId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId_PaymentDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId_Status_PaymentDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_CustomerId_CreatedAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_Status_CreatedAt",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventories_CurrentStock_NonNegative",
                table: "Inventories");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_City",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_BillingTransactions_IdempotencyRecordId",
                table: "BillingTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BillingTransactions_Provider_TransactionId",
                table: "BillingTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BillingTransactions_TenantId_Status_CreatedAt",
                table: "BillingTransactions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderOperationKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "IdempotencyRecordId",
                table: "BillingTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderOperationKey",
                table: "BillingTransactions");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "BillingTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_Status",
                table: "Orders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingTransactions_TransactionId",
                table: "BillingTransactions",
                column: "TransactionId");
        }
    }
}

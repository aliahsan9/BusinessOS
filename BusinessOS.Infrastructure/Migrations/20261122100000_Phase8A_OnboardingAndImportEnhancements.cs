using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Phase8A_OnboardingAndImportEnhancements : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ContentHash",
            table: "DataImportBatches",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FailedRowsJson",
            table: "DataImportBatches",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_DataImportBatches_TenantId_EntityType_ContentHash",
            table: "DataImportBatches",
            columns: new[] { "TenantId", "EntityType", "ContentHash" });

        migrationBuilder.AddColumn<string>(
            name: "ExternalImportKey",
            table: "Orders",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Orders_TenantId_ExternalImportKey",
            table: "Orders",
            columns: new[] { "TenantId", "ExternalImportKey" },
            unique: true,
            filter: "[ExternalImportKey] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.AddColumn<string>(
            name: "ExternalImportKey",
            table: "Expenses",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_TenantId_ExternalImportKey",
            table: "Expenses",
            columns: new[] { "TenantId", "ExternalImportKey" },
            unique: true,
            filter: "[ExternalImportKey] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.AddColumn<string>(
            name: "ExternalImportKey",
            table: "Payments",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Payments_TenantId_ExternalImportKey",
            table: "Payments",
            columns: new[] { "TenantId", "ExternalImportKey" },
            unique: true,
            filter: "[ExternalImportKey] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.AddColumn<string>(
            name: "ExternalImportKey",
            table: "PurchaseInvoices",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseInvoices_TenantId_ExternalImportKey",
            table: "PurchaseInvoices",
            columns: new[] { "TenantId", "ExternalImportKey" },
            unique: true,
            filter: "[ExternalImportKey] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.AddColumn<int>(
            name: "FinancialYearStartMonth",
            table: "TenantSettings",
            type: "int",
            nullable: false,
            defaultValue: 7);

        migrationBuilder.AddColumn<string>(
            name: "CompletedStepsJson",
            table: "UserOnboardingProgress",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastResumedAt",
            table: "UserOnboardingProgress",
            type: "datetime2",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_DataImportBatches_TenantId_EntityType_ContentHash",
            table: "DataImportBatches");
        migrationBuilder.DropColumn(name: "ContentHash", table: "DataImportBatches");
        migrationBuilder.DropColumn(name: "FailedRowsJson", table: "DataImportBatches");

        migrationBuilder.DropIndex(name: "IX_Orders_TenantId_ExternalImportKey", table: "Orders");
        migrationBuilder.DropColumn(name: "ExternalImportKey", table: "Orders");

        migrationBuilder.DropIndex(name: "IX_Expenses_TenantId_ExternalImportKey", table: "Expenses");
        migrationBuilder.DropColumn(name: "ExternalImportKey", table: "Expenses");

        migrationBuilder.DropIndex(name: "IX_Payments_TenantId_ExternalImportKey", table: "Payments");
        migrationBuilder.DropColumn(name: "ExternalImportKey", table: "Payments");

        migrationBuilder.DropIndex(name: "IX_PurchaseInvoices_TenantId_ExternalImportKey", table: "PurchaseInvoices");
        migrationBuilder.DropColumn(name: "ExternalImportKey", table: "PurchaseInvoices");

        migrationBuilder.DropColumn(name: "FinancialYearStartMonth", table: "TenantSettings");
        migrationBuilder.DropColumn(name: "CompletedStepsJson", table: "UserOnboardingProgress");
        migrationBuilder.DropColumn(name: "LastResumedAt", table: "UserOnboardingProgress");
    }
}

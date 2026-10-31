using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InternationalBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AiInputTokens",
                table: "TenantUsage",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "AiOutputTokens",
                table: "TenantUsage",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "AiPeriodStart",
                table: "TenantUsage",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AiTotalTokens",
                table: "TenantUsage",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedProviderCostUsd",
                table: "TenantUsage",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ProductCount",
                table: "TenantUsage",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AiRequestAllowance",
                table: "TenantSubscriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRenewEnabled",
                table: "TenantSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BillingCountry",
                table: "TenantSubscriptions",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelEffectiveAt",
                table: "TenantSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractedAnnualPrice",
                table: "TenantSubscriptions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractedCurrency",
                table: "TenantSubscriptions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractedLimitsJson",
                table: "TenantSubscriptions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractedMonthlyPrice",
                table: "TenantSubscriptions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayCurrency",
                table: "TenantSubscriptions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodSummary",
                table: "TenantSubscriptions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreserveContract",
                table: "TenantSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduledPlanId",
                table: "TenantSubscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementCurrency",
                table: "TenantSubscriptions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrialConsumed",
                table: "TenantSubscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "MonthlyPrice",
                table: "SubscriptionPlans",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "AnnualPrice",
                table: "SubscriptionPlans",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "BaseCurrency",
                table: "SubscriptionPlans",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CatalogRevision",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasApiAccess",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasMultiBranch",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InventoryAccess",
                table: "SubscriptionPlans",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomPricing",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "SubscriptionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxProducts",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SalesAccess",
                table: "SubscriptionPlans",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TrialDays",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MerchantCountry",
                table: "PaymentProviders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportedCurrencies",
                table: "PaymentProviders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsOneTime",
                table: "PaymentProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsRecurring",
                table: "PaymentProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsRefunds",
                table: "PaymentProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsTokenization",
                table: "PaymentProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BillingTransactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "BillingTransactions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "BillingInvoices",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                table: "BillingInvoices",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "BillingInvoices",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.CreateTable(
                name: "AiUsageLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: false),
                    EstimatedProviderCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ProviderFailed = table.Column<bool>(type: "bit", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiUsageLedgers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillingPaymentMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Last4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    ExpMonth = table.Column<int>(type: "int", nullable: true),
                    ExpYear = table.Column<int>(type: "int", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SupportsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingPaymentMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingPaymentMethods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegionalPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AnnualAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ProviderPriceIdMonthly = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderPriceIdAnnual = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExchangeRateNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionalPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegionalPrices_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionHistory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageLedgers_TenantId_OperationKey",
                table: "AiUsageLedgers",
                columns: new[] { "TenantId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingPaymentMethods_TenantId_Provider_ExternalId",
                table: "BillingPaymentMethods",
                columns: new[] { "TenantId", "Provider", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegionalPrices_SubscriptionPlanId_CountryCode_CurrencyCode",
                table: "RegionalPrices",
                columns: new[] { "SubscriptionPlanId", "CountryCode", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionHistory_TenantId_CreatedAt",
                table: "SubscriptionHistory",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageLedgers");

            migrationBuilder.DropTable(
                name: "BillingPaymentMethods");

            migrationBuilder.DropTable(
                name: "RegionalPrices");

            migrationBuilder.DropTable(
                name: "SubscriptionHistory");

            migrationBuilder.DropColumn(
                name: "AiInputTokens",
                table: "TenantUsage");

            migrationBuilder.DropColumn(
                name: "AiOutputTokens",
                table: "TenantUsage");

            migrationBuilder.DropColumn(
                name: "AiPeriodStart",
                table: "TenantUsage");

            migrationBuilder.DropColumn(
                name: "AiTotalTokens",
                table: "TenantUsage");

            migrationBuilder.DropColumn(
                name: "EstimatedProviderCostUsd",
                table: "TenantUsage");

            migrationBuilder.DropColumn(
                name: "ProductCount",
                table: "TenantUsage");

            migrationBuilder.DropColumn(
                name: "AiRequestAllowance",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "AutoRenewEnabled",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "BillingCountry",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "CancelEffectiveAt",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "ContractedAnnualPrice",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "ContractedCurrency",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "ContractedLimitsJson",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "ContractedMonthlyPrice",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "DisplayCurrency",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentMethodSummary",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "PreserveContract",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "ScheduledPlanId",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "SettlementCurrency",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "TrialConsumed",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "BaseCurrency",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "CatalogRevision",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasApiAccess",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasMultiBranch",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "InventoryAccess",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "IsCustomPricing",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MaxProducts",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "SalesAccess",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "TrialDays",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MerchantCountry",
                table: "PaymentProviders");

            migrationBuilder.DropColumn(
                name: "SupportedCurrencies",
                table: "PaymentProviders");

            migrationBuilder.DropColumn(
                name: "SupportsOneTime",
                table: "PaymentProviders");

            migrationBuilder.DropColumn(
                name: "SupportsRecurring",
                table: "PaymentProviders");

            migrationBuilder.DropColumn(
                name: "SupportsRefunds",
                table: "PaymentProviders");

            migrationBuilder.DropColumn(
                name: "SupportsTokenization",
                table: "PaymentProviders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "BillingTransactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "MonthlyPrice",
                table: "SubscriptionPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "AnnualPrice",
                table: "SubscriptionPlans",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "BillingTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "BillingInvoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                table: "BillingInvoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "BillingInvoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);
        }
    }
}

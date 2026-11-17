using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6CommercialReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgentExecutionsUsed",
                table: "TenantUsage",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AutomationRunsUsed",
                table: "TenantUsage",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "GraceEndsAt",
                table: "TenantSubscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GracePeriodDays",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxAgentExecutions",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.AddColumn<int>(
                name: "MaxAutomationRuns",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.AddColumn<string>(
                name: "BillToName",
                table: "BillingInvoices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "BillingInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                table: "BillingInvoices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualProviderChargeUsd",
                table: "AiUsageLedgers",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentKey",
                table: "AiUsageLedgers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "AiUsageLedgers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsageCategory",
                table: "AiUsageLedgers",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "AiUsageLedgers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiModelPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UsageCategory = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    InputUsdPerMillion = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    OutputUsdPerMillion = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PricingVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyFeatureOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Limit = table.Column<long>(type: "bigint", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyFeatureOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyFeatureOverrides_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyStorefronts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    Tagline = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    PublicDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublicEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PublicPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SocialLinksJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HoursJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BrandColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    Layout = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ShowProducts = table.Column<bool>(type: "bit", nullable: false),
                    ShowServices = table.Column<bool>(type: "bit", nullable: false),
                    ShowPortfolio = table.Column<bool>(type: "bit", nullable: false),
                    ShowReviews = table.Column<bool>(type: "bit", nullable: false),
                    SeoTitle = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    SeoDescription = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ConsentNotice = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PreferredWorkspace = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyStorefronts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DashboardPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    WidgetsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanFeatureGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Limit = table.Column<long>(type: "bigint", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatureGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanFeatureGrants_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontInquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BudgetRange = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Requirements = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientToken = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontInquiries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontOfferings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PricingModel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Deliverables = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DeliveryTime = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontOfferings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Deliverables = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PricingModel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontPortfolioItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Technologies = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ProjectUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Results = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontPortfolioItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReportReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontSlugRedirects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromSlug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontSlugRedirects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPortalAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPortalAccesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageAlerts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiModelPrices_Model_IsActive_EffectiveFrom",
                table: "AiModelPrices",
                columns: new[] { "Model", "IsActive", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyFeatureOverrides_TenantId_FeatureKey_EffectiveFrom",
                table: "CompanyFeatureOverrides",
                columns: new[] { "TenantId", "FeatureKey", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyStorefronts_TenantId",
                table: "CompanyStorefronts",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardPreferences_TenantId_UserId",
                table: "DashboardPreferences",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatureGrants_SubscriptionPlanId_FeatureKey_EffectiveFrom",
                table: "PlanFeatureGrants",
                columns: new[] { "SubscriptionPlanId", "FeatureKey", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontInquiries_TenantId_ClientToken",
                table: "StorefrontInquiries",
                columns: new[] { "TenantId", "ClientToken" });

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontOfferings_TenantId_IsPublished",
                table: "StorefrontOfferings",
                columns: new[] { "TenantId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontSlugRedirects_FromSlug",
                table: "StorefrontSlugRedirects",
                column: "FromSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPortalAccesses_TokenHash",
                table: "SupplierPortalAccesses",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageAlerts_TenantId_Resource_Level_PeriodStart",
                table: "UsageAlerts",
                columns: new[] { "TenantId", "Resource", "Level", "PeriodStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiModelPrices");

            migrationBuilder.DropTable(
                name: "CompanyFeatureOverrides");

            migrationBuilder.DropTable(
                name: "CompanyStorefronts");

            migrationBuilder.DropTable(
                name: "DashboardPreferences");

            migrationBuilder.DropTable(
                name: "PlanFeatureGrants");

            migrationBuilder.DropTable(
                name: "StorefrontInquiries");

            migrationBuilder.DropTable(
                name: "StorefrontOfferings");

            migrationBuilder.DropTable(
                name: "StorefrontPackages");

            migrationBuilder.DropTable(
                name: "StorefrontPortfolioItems");

            migrationBuilder.DropTable(
                name: "StorefrontReviews");

            migrationBuilder.DropTable(
                name: "StorefrontSlugRedirects");

            migrationBuilder.DropTable(
                name: "SupplierPortalAccesses");

            migrationBuilder.DropTable(
                name: "UsageAlerts");

            migrationBuilder.DropColumn(
                name: "AgentExecutionsUsed",
                table: "TenantUsage");

            migrationBuilder.DropColumn(
                name: "AutomationRunsUsed",
                table: "TenantUsage");

            migrationBuilder.DropColumn(
                name: "GraceEndsAt",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "GracePeriodDays",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MaxAgentExecutions",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "MaxAutomationRuns",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "BillToName",
                table: "BillingInvoices");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "BillingInvoices");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
                table: "BillingInvoices");

            migrationBuilder.DropColumn(
                name: "ActualProviderChargeUsd",
                table: "AiUsageLedgers");

            migrationBuilder.DropColumn(
                name: "AgentKey",
                table: "AiUsageLedgers");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "AiUsageLedgers");

            migrationBuilder.DropColumn(
                name: "UsageCategory",
                table: "AiUsageLedgers");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AiUsageLedgers");
        }
    }
}

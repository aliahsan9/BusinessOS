using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5DInventoryProcurementAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalFingerprint",
                table: "Purchases",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DelayedNotifiedAt",
                table: "Purchases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDeliveryDate",
                table: "Purchases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Purchases",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "Purchases",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SentToSupplier",
                table: "Purchases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DamagedQuantity",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQuantity",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReceivingNotes",
                table: "PurchaseItems",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShortageQuantity",
                table: "PurchaseItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "MarketingAgentPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequireApprovalBeforePublish = table.Column<bool>(type: "bit", nullable: false),
                    DailyPublishLimit = table.Column<int>(type: "int", nullable: false),
                    WeeklyAiBudgetUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    MonthlyAiBudgetUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DailyCronEnabled = table.Column<bool>(type: "bit", nullable: false),
                    WeeklyCronEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MonthlyCronEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PausedReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingAgentPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingAnalyticsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalPostId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MetricsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataSource = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UnavailableMetricsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingAnalyticsSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingBrandProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Industry = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ProductsServicesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    TargetAudience = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BrandDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BrandVoice = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BrandColorsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SocialAccountsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MarketingObjectivesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PreferredLanguagesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentRestrictions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompetitorsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MarketingPreferencesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SourceNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingBrandProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TargetAudience = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ContentThemesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecommendedPlatformsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PublishingFrequency = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ContentFormatsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuccessMetricsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EstimatedAiCostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StrategyBody = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    StrategyApproved = table.Column<bool>(type: "bit", nullable: false),
                    StrategyApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StrategyApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingContentAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    Prompt = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Brief = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    StorageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MimeType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BrandCompliant = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingContentAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingContentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    HashtagsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CallToAction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SuggestedPublishAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledPublishAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssociatedCampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImageAssetIdsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovalFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RejectedReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ConnectedAccountProvider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ConnectedAccountExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalPostId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingContentItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingPublishJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AccountExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExternalPostId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClaimToken = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingPublishJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketingResearchReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    SourcesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CompetitorNamesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ThemesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    KeywordsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OpportunitiesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Fabricated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketingResearchReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementAgentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyInventoryMonitoring = table.Column<bool>(type: "bit", nullable: false),
                    LowStockDetection = table.Column<bool>(type: "bit", nullable: false),
                    DemandAnalysis = table.Column<bool>(type: "bit", nullable: false),
                    ProcurementRecommendations = table.Column<bool>(type: "bit", nullable: false),
                    PurchaseOrderStatusMonitoring = table.Column<bool>(type: "bit", nullable: false),
                    DeliveryFollowUps = table.Column<bool>(type: "bit", nullable: false),
                    InventoryDiscrepancyAlerts = table.Column<bool>(type: "bit", nullable: false),
                    SlowMovingDays = table.Column<int>(type: "int", nullable: false),
                    OverstockMultiplier = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumHistoryDays = table.Column<int>(type: "int", nullable: false),
                    PreferredSupplierCriterion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementAgentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Confidence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovalFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PurchaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AssumptionsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRecommendations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRecommendations_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProcurementRecommendations_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReceivingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    StockIncreasedBy = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ResultStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceivingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReceivingEvents_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesAgentObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObjectiveText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TargetProspectCount = table.Column<int>(type: "int", nullable: false),
                    IdealCustomerProfileJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DelegatedByAgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ParentExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DiscoveredCount = table.Column<int>(type: "int", nullable: false),
                    QualifiedCount = table.Column<int>(type: "int", nullable: false),
                    OutreachDraftCount = table.Column<int>(type: "int", nullable: false),
                    OutreachSentCount = table.Column<int>(type: "int", nullable: false),
                    ResponseCount = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAgentObjectives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesAgentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    DailyResearchLimit = table.Column<int>(type: "int", nullable: false),
                    MaxOutreachPerDay = table.Column<int>(type: "int", nullable: false),
                    AllowFixtureResearch = table.Column<bool>(type: "bit", nullable: false),
                    IdealCustomerProfileJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QualificationCriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QualificationThreshold = table.Column<int>(type: "int", nullable: false),
                    ResearchProviderKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ResearchApiKeyProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResearchBaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CurrentObjective = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActiveObjectiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequireOutreachApproval = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesAgentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesEmailSuppressions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesEmailSuppressions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesOutboundEmailLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutreachMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Performed = table.Column<bool>(type: "bit", nullable: false),
                    IsTestSink = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOutboundEmailLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesOutreachCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObjectiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SequenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DraftCount = table.Column<int>(type: "int", nullable: false),
                    PendingApprovalCount = table.Column<int>(type: "int", nullable: false),
                    SentCount = table.Column<int>(type: "int", nullable: false),
                    ResponseCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOutreachCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesOutreachMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProspectResearchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FitExplanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SequenceStep = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UsedTestSink = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOutreachMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesProspectResearch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObjectiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Website = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CompanySize = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    BusinessDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublicContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    PublicContactPhone = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DecisionMakerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecisionMakerTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProviderKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceAttribution = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResearchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeadScore = table.Column<int>(type: "int", nullable: true),
                    ScoreExplanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ScoreIsEstimate = table.Column<bool>(type: "bit", nullable: false),
                    IsQualified = table.Column<bool>(type: "bit", nullable: false),
                    IsDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    DedupKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    QualityStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    QualityNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesProspectResearch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportChannelAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowsOutbound = table.Column<bool>(type: "bit", nullable: false),
                    AllowsInbound = table.Column<bool>(type: "bit", nullable: false),
                    LastInboundAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutboundAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportChannelAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AssignedAgentKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AssignedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ExternalThreadId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    AiSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecommendedAction = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EscalationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EscalationSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCustomerMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAgentMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RelatedOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HumanTakeover = table.Column<bool>(type: "bit", nullable: false),
                    MarketingConsentForHandoff = table.Column<bool>(type: "bit", nullable: false),
                    ReopenCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportFollowUpJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MessageTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DedupKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RespectBusinessHours = table.Column<bool>(type: "bit", nullable: false),
                    OptOutHonored = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportFollowUpJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportFollowUpJobs_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupportMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IsInternalNote = table.Column<bool>(type: "bit", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportMessages_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_TenantId_ExpectedDeliveryDate",
                table: "Purchases",
                columns: new[] { "TenantId", "ExpectedDeliveryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_TenantId_IdempotencyKey",
                table: "Purchases",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingAgentPolicies_TenantId",
                table: "MarketingAgentPolicies",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketingAnalyticsSnapshots_TenantId_CampaignId",
                table: "MarketingAnalyticsSnapshots",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingAnalyticsSnapshots_TenantId_ContentItemId",
                table: "MarketingAnalyticsSnapshots",
                columns: new[] { "TenantId", "ContentItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingAnalyticsSnapshots_TenantId_SyncedAt",
                table: "MarketingAnalyticsSnapshots",
                columns: new[] { "TenantId", "SyncedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingBrandProfiles_TenantId",
                table: "MarketingBrandProfiles",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketingCampaigns_TenantId_Status",
                table: "MarketingCampaigns",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingContentAssets_TenantId_CampaignId",
                table: "MarketingContentAssets",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingContentAssets_TenantId_ContentItemId",
                table: "MarketingContentAssets",
                columns: new[] { "TenantId", "ContentItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingContentItems_TenantId_ApprovalStatus",
                table: "MarketingContentItems",
                columns: new[] { "TenantId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingContentItems_TenantId_CampaignId",
                table: "MarketingContentItems",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingContentItems_TenantId_IdempotencyKey",
                table: "MarketingContentItems",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MarketingContentItems_TenantId_SuggestedPublishAt",
                table: "MarketingContentItems",
                columns: new[] { "TenantId", "SuggestedPublishAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingPublishJobs_TenantId_ContentItemId",
                table: "MarketingPublishJobs",
                columns: new[] { "TenantId", "ContentItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingPublishJobs_TenantId_IdempotencyKey",
                table: "MarketingPublishJobs",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketingPublishJobs_TenantId_Status_RunAtUtc",
                table: "MarketingPublishJobs",
                columns: new[] { "TenantId", "Status", "RunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingResearchReports_TenantId_Kind",
                table: "MarketingResearchReports",
                columns: new[] { "TenantId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementAgentSettings_TenantId",
                table: "ProcurementAgentSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRecommendations_ProductId",
                table: "ProcurementRecommendations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRecommendations_PurchaseId",
                table: "ProcurementRecommendations",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRecommendations_SupplierId",
                table: "ProcurementRecommendations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRecommendations_TenantId_IdempotencyKey",
                table: "ProcurementRecommendations",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRecommendations_TenantId_ProductId",
                table: "ProcurementRecommendations",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRecommendations_TenantId_Status",
                table: "ProcurementRecommendations",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceivingEvents_PurchaseId",
                table: "PurchaseReceivingEvents",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceivingEvents_TenantId_PurchaseId",
                table: "PurchaseReceivingEvents",
                columns: new[] { "TenantId", "PurchaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAgentObjectives_TenantId_Status_CreatedAt",
                table: "SalesAgentObjectives",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesAgentSettings_TenantId",
                table: "SalesAgentSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesEmailSuppressions_TenantId_Email",
                table: "SalesEmailSuppressions",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOutboundEmailLogs_TenantId_CreatedAt",
                table: "SalesOutboundEmailLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOutboundEmailLogs_TenantId_OutreachMessageId",
                table: "SalesOutboundEmailLogs",
                columns: new[] { "TenantId", "OutreachMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOutreachCampaigns_TenantId_Status_CreatedAt",
                table: "SalesOutreachCampaigns",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOutreachMessages_TenantId_CampaignId",
                table: "SalesOutreachMessages",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOutreachMessages_TenantId_IdempotencyKey",
                table: "SalesOutreachMessages",
                columns: new[] { "TenantId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOutreachMessages_TenantId_Status_CreatedAt",
                table: "SalesOutreachMessages",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesProspectResearch_TenantId_DedupKey",
                table: "SalesProspectResearch",
                columns: new[] { "TenantId", "DedupKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesProspectResearch_TenantId_IsQualified",
                table: "SalesProspectResearch",
                columns: new[] { "TenantId", "IsQualified" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesProspectResearch_TenantId_ResearchedAt",
                table: "SalesProspectResearch",
                columns: new[] { "TenantId", "ResearchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportChannelAccounts_TenantId_Channel",
                table: "SupportChannelAccounts",
                columns: new[] { "TenantId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportFollowUpJobs_TenantId_DedupKey",
                table: "SupportFollowUpJobs",
                columns: new[] { "TenantId", "DedupKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportFollowUpJobs_TenantId_Status_DueAt",
                table: "SupportFollowUpJobs",
                columns: new[] { "TenantId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportFollowUpJobs_TicketId",
                table: "SupportFollowUpJobs",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_TenantId_ExternalMessageId",
                table: "SupportMessages",
                columns: new[] { "TenantId", "ExternalMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_TenantId_IdempotencyKey",
                table: "SupportMessages",
                columns: new[] { "TenantId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_TenantId_TicketId_CreatedAt",
                table: "SupportMessages",
                columns: new[] { "TenantId", "TicketId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_TicketId",
                table: "SupportMessages",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId_ExternalThreadId",
                table: "SupportTickets",
                columns: new[] { "TenantId", "ExternalThreadId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId_IdempotencyKey",
                table: "SupportTickets",
                columns: new[] { "TenantId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId_Status_Priority",
                table: "SupportTickets",
                columns: new[] { "TenantId", "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId_TicketNumber",
                table: "SupportTickets",
                columns: new[] { "TenantId", "TicketNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketingAgentPolicies");

            migrationBuilder.DropTable(
                name: "MarketingAnalyticsSnapshots");

            migrationBuilder.DropTable(
                name: "MarketingBrandProfiles");

            migrationBuilder.DropTable(
                name: "MarketingCampaigns");

            migrationBuilder.DropTable(
                name: "MarketingContentAssets");

            migrationBuilder.DropTable(
                name: "MarketingContentItems");

            migrationBuilder.DropTable(
                name: "MarketingPublishJobs");

            migrationBuilder.DropTable(
                name: "MarketingResearchReports");

            migrationBuilder.DropTable(
                name: "ProcurementAgentSettings");

            migrationBuilder.DropTable(
                name: "ProcurementRecommendations");

            migrationBuilder.DropTable(
                name: "PurchaseReceivingEvents");

            migrationBuilder.DropTable(
                name: "SalesAgentObjectives");

            migrationBuilder.DropTable(
                name: "SalesAgentSettings");

            migrationBuilder.DropTable(
                name: "SalesEmailSuppressions");

            migrationBuilder.DropTable(
                name: "SalesOutboundEmailLogs");

            migrationBuilder.DropTable(
                name: "SalesOutreachCampaigns");

            migrationBuilder.DropTable(
                name: "SalesOutreachMessages");

            migrationBuilder.DropTable(
                name: "SalesProspectResearch");

            migrationBuilder.DropTable(
                name: "SupportChannelAccounts");

            migrationBuilder.DropTable(
                name: "SupportFollowUpJobs");

            migrationBuilder.DropTable(
                name: "SupportMessages");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_TenantId_ExpectedDeliveryDate",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_TenantId_IdempotencyKey",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "ApprovalFingerprint",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "DelayedNotifiedAt",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "ExpectedDeliveryDate",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "SentToSupplier",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "DamagedQuantity",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "ReceivingNotes",
                table: "PurchaseItems");

            migrationBuilder.DropColumn(
                name: "ShortageQuantity",
                table: "PurchaseItems");
        }
    }
}

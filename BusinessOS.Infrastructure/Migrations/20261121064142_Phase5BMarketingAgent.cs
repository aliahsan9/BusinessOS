using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5BMarketingAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    BrandDescription = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: true),
                    BrandVoice = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BrandColorsJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SocialAccountsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
                    MarketingObjectivesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
                    PreferredLanguagesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentRestrictions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompetitorsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
                    MarketingPreferencesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
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
                    ContentThemesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
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
                    Prompt = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: true),
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
                    Caption = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: true),
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
                    CompetitorNamesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
                    ThemesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
                    KeywordsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
                    OpportunitiesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 4000, nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MarketingAgentPolicies");
            migrationBuilder.DropTable(name: "MarketingAnalyticsSnapshots");
            migrationBuilder.DropTable(name: "MarketingBrandProfiles");
            migrationBuilder.DropTable(name: "MarketingCampaigns");
            migrationBuilder.DropTable(name: "MarketingContentAssets");
            migrationBuilder.DropTable(name: "MarketingContentItems");
            migrationBuilder.DropTable(name: "MarketingPublishJobs");
            migrationBuilder.DropTable(name: "MarketingResearchReports");
        }
    }
}

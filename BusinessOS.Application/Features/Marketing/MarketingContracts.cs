namespace BusinessOS.Application.Features.Marketing;

public sealed class UpsertBrandProfileRequest
{
    public string? BusinessName { get; set; }
    public string? Industry { get; set; }
    public string? ProductsServicesJson { get; set; }
    public string? TargetAudience { get; set; }
    public string? BrandDescription { get; set; }
    public string? BrandVoice { get; set; }
    public string? BrandColorsJson { get; set; }
    public string? LogoUrl { get; set; }
    public string? Website { get; set; }
    public string? SocialAccountsJson { get; set; }
    public string? MarketingObjectivesJson { get; set; }
    public string? PreferredLanguagesJson { get; set; }
    public string? ContentRestrictions { get; set; }
    public string? CompetitorsJson { get; set; }
    public string? MarketingPreferencesJson { get; set; }
    public string? SourceNotes { get; set; }
}

public sealed class GenerateStrategyRequest
{
    public Guid? CampaignId { get; set; }
    public string? Title { get; set; }
    public string? Objective { get; set; }
    public string? TargetAudience { get; set; }
    public string? AgentKey { get; set; }
}

public sealed class UpdateStrategyRequest
{
    public string? StrategyBody { get; set; }
    public string? ContentThemesJson { get; set; }
    public string? RecommendedPlatformsJson { get; set; }
    public string? PublishingFrequency { get; set; }
    public string? ContentFormatsJson { get; set; }
    public string? SuccessMetricsJson { get; set; }
}

public sealed class CreateCampaignRequest
{
    public string Title { get; set; } = "";
    public string? Objective { get; set; }
    public string? TargetAudience { get; set; }
    public string? ContentThemesJson { get; set; }
    public string? RecommendedPlatformsJson { get; set; }
    public string? PublishingFrequency { get; set; }
    public string? ContentFormatsJson { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SuccessMetricsJson { get; set; }
    public string? StrategyBody { get; set; }
    public string? AgentKey { get; set; }
}

public sealed class GenerateContentCalendarRequest
{
    public Guid? CampaignId { get; set; }
    public int Days { get; set; } = 7;
    public IReadOnlyList<string>? Platforms { get; set; }
    public DateTime? StartDate { get; set; }
    public string? ThemesJson { get; set; }
    public string? AgentKey { get; set; }
}

public sealed class GenerateContentItemRequest
{
    public Guid? CampaignId { get; set; }
    public string? Title { get; set; }
    public string? Platform { get; set; }
    public string? ContentType { get; set; }
    public string? Brief { get; set; }
    public DateTime? SuggestedPublishAt { get; set; }
    public string? AgentKey { get; set; }
    public Guid? ExecutionId { get; set; }
}

public sealed class UpdateContentItemRequest
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Caption { get; set; }
    public string? HashtagsJson { get; set; }
    public string? CallToAction { get; set; }
    public DateTime? SuggestedPublishAt { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public string? ImageAssetIdsJson { get; set; }
    public string? ConnectedAccountProvider { get; set; }
    public string? ConnectedAccountExternalId { get; set; }
}

public sealed class GenerateGraphicRequest
{
    public Guid? ContentItemId { get; set; }
    public Guid? CampaignId { get; set; }
    public string? Platform { get; set; }
    public string? Prompt { get; set; }
    public string? Brief { get; set; }
}

public sealed class CreateResearchReportRequest
{
    public string Kind { get; set; } = "Competitor";
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public string? AgentKey { get; set; }
}

public sealed class ScheduleContentRequest
{
    public DateTime? RunAtUtc { get; set; }
    public string? ConnectedAccountProvider { get; set; }
    public string? ConnectedAccountExternalId { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class RejectContentRequest
{
    public string? Reason { get; set; }
}

public sealed class MarketingPolicyWriteRequest
{
    public bool? RequireApprovalBeforePublish { get; set; }
    public int? DailyPublishLimit { get; set; }
    public decimal? WeeklyAiBudgetUsd { get; set; }
    public decimal? MonthlyAiBudgetUsd { get; set; }
    public string? TimeZoneId { get; set; }
    public bool? DailyCronEnabled { get; set; }
    public bool? WeeklyCronEnabled { get; set; }
    public bool? MonthlyCronEnabled { get; set; }
    public bool? IsPaused { get; set; }
    public string? PausedReason { get; set; }
}

public sealed class SyncAnalyticsRequest
{
    public Guid? ContentItemId { get; set; }
    public Guid? CampaignId { get; set; }
    public bool AllowSandbox { get; set; }
}

public sealed record MarketingBrandProfileDto(
    Guid Id,
    string BusinessName,
    string? Industry,
    string ProductsServicesJson,
    string? TargetAudience,
    string? BrandDescription,
    string? BrandVoice,
    string BrandColorsJson,
    string? LogoUrl,
    string? Website,
    string SocialAccountsJson,
    string MarketingObjectivesJson,
    string PreferredLanguagesJson,
    string? ContentRestrictions,
    string CompetitorsJson,
    string MarketingPreferencesJson,
    string? SourceNotes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record MarketingCampaignDto(
    Guid Id,
    string Title,
    string Objective,
    string? TargetAudience,
    string ContentThemesJson,
    string RecommendedPlatformsJson,
    string? PublishingFrequency,
    string ContentFormatsJson,
    DateTime? StartDate,
    DateTime? EndDate,
    string SuccessMetricsJson,
    decimal EstimatedAiCostUsd,
    string Status,
    string? StrategyBody,
    bool StrategyApproved,
    DateTime? StrategyApprovedAt,
    string? StrategyApprovedByUserId,
    string? AgentKey,
    DateTime CreatedAt);

public sealed record MarketingContentItemDto(
    Guid Id,
    Guid? CampaignId,
    string Title,
    string Body,
    string Platform,
    string ContentType,
    string? Caption,
    string HashtagsJson,
    string? CallToAction,
    DateTime? SuggestedPublishAt,
    DateTime? ScheduledPublishAt,
    string ImageAssetIdsJson,
    string ApprovalStatus,
    string? ApprovalFingerprint,
    DateTime? ApprovedAt,
    string? ApprovedByUserId,
    string? RejectedReason,
    string? ConnectedAccountProvider,
    string? ConnectedAccountExternalId,
    string? ExternalPostId,
    string? LastError,
    string? IdempotencyKey,
    string? AgentKey,
    DateTime CreatedAt);

public sealed record MarketingContentAssetDto(
    Guid Id,
    Guid? ContentItemId,
    Guid? CampaignId,
    string Kind,
    string? Platform,
    int? Width,
    int? Height,
    string? Prompt,
    string? Brief,
    string? StorageUrl,
    string? StoragePath,
    string? MimeType,
    string Source,
    bool BrandCompliant,
    string Status,
    DateTime CreatedAt);

public sealed record MarketingResearchReportDto(
    Guid Id,
    string Kind,
    string Title,
    string Body,
    string SourcesJson,
    string CompetitorNamesJson,
    string ThemesJson,
    string KeywordsJson,
    string OpportunitiesJson,
    DateTime GeneratedAt,
    string? AgentKey,
    bool Fabricated);

public sealed record MarketingPublishJobDto(
    Guid Id,
    Guid ContentItemId,
    string Provider,
    string AccountExternalId,
    DateTime RunAtUtc,
    string Status,
    int Attempt,
    string IdempotencyKey,
    string? LastError,
    string? ExternalPostId,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public sealed record MarketingAnalyticsSnapshotDto(
    Guid Id,
    Guid? ContentItemId,
    Guid? CampaignId,
    string Provider,
    string? ExternalPostId,
    string? MetricsJson,
    DateTime SyncedAt,
    string DataSource,
    string UnavailableMetricsJson);

public sealed record MarketingAgentPolicyDto(
    Guid Id,
    bool RequireApprovalBeforePublish,
    int DailyPublishLimit,
    decimal WeeklyAiBudgetUsd,
    decimal MonthlyAiBudgetUsd,
    string TimeZoneId,
    bool DailyCronEnabled,
    bool WeeklyCronEnabled,
    bool MonthlyCronEnabled,
    string? PausedReason,
    bool IsPaused);

public sealed record MarketingOverviewDto(
    int Campaigns,
    int ActiveCampaigns,
    int ContentDrafts,
    int ContentInReview,
    int ContentApproved,
    int ContentScheduled,
    int ContentPublished,
    int ResearchReports,
    int PublishJobsPending,
    int AnalyticsSnapshots,
    bool HasBrandProfile,
    string DataClassification);

public sealed record MarketingConnectedAccountDto(
    string Provider,
    string? ConnectionId,
    string Status,
    string? ExternalAccountId,
    string? ExternalAccountLabel,
    bool PublishAvailable,
    string Notes);

public sealed record MarketingCalendarDto(
    DateTime From,
    DateTime To,
    string View,
    IReadOnlyList<MarketingContentItemDto> Items);

public sealed record MarketingPerformanceReportDto(
    string Title,
    string Body,
    string VerifiedMetricsJson,
    string UnavailableMetricsJson,
    string DataSourcesJson,
    DateTime GeneratedAt,
    bool Fabricated);

public sealed record MarketingSyncAnalyticsResultDto(
    int SnapshotsCreated,
    string DataSource,
    string Message,
    IReadOnlyList<MarketingAnalyticsSnapshotDto> Snapshots);

public sealed record MarketingPublishProcessResultDto(
    int Claimed,
    int Succeeded,
    int Failed,
    int Skipped);

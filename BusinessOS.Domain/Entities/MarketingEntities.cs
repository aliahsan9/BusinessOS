using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class MarketingBrandProfile : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string BusinessName { get; set; } = default!;
    public string? Industry { get; set; }
    public string ProductsServicesJson { get; set; } = "[]";
    public string? TargetAudience { get; set; }
    public string? BrandDescription { get; set; }
    public string? BrandVoice { get; set; }
    public string BrandColorsJson { get; set; } = "[]";
    public string? LogoUrl { get; set; }
    public string? Website { get; set; }
    public string SocialAccountsJson { get; set; } = "{}";
    public string MarketingObjectivesJson { get; set; } = "[]";
    public string PreferredLanguagesJson { get; set; } = "[\"en\"]";
    public string? ContentRestrictions { get; set; }
    public string CompetitorsJson { get; set; } = "[]";
    public string MarketingPreferencesJson { get; set; } = "{}";
    public string? SourceNotes { get; set; }
}

public class MarketingCampaign : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = default!;
    public string Objective { get; set; } = "BrandAwareness";
    public string? TargetAudience { get; set; }
    public string ContentThemesJson { get; set; } = "[]";
    public string RecommendedPlatformsJson { get; set; } = "[]";
    public string? PublishingFrequency { get; set; }
    public string ContentFormatsJson { get; set; } = "[]";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string SuccessMetricsJson { get; set; } = "[]";
    public decimal EstimatedAiCostUsd { get; set; }
    public string Status { get; set; } = "Draft";
    public string? StrategyBody { get; set; }
    public bool StrategyApproved { get; set; }
    public DateTime? StrategyApprovedAt { get; set; }
    public string? StrategyApprovedByUserId { get; set; }
    public string? AgentKey { get; set; }
}

public class MarketingContentItem : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? CampaignId { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = "";
    public string Platform { get; set; } = "Other";
    public string ContentType { get; set; } = "Post";
    public string? Caption { get; set; }
    public string HashtagsJson { get; set; } = "[]";
    public string? CallToAction { get; set; }
    public DateTime? SuggestedPublishAt { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public Guid? AssociatedCampaignId { get; set; }
    public string ImageAssetIdsJson { get; set; } = "[]";
    public string ApprovalStatus { get; set; } = "Draft";
    public string? ApprovalFingerprint { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByUserId { get; set; }
    public string? RejectedReason { get; set; }
    public string? ConnectedAccountProvider { get; set; }
    public string? ConnectedAccountExternalId { get; set; }
    public string? ExternalPostId { get; set; }
    public string? LastError { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? AgentKey { get; set; }
    public Guid? ExecutionId { get; set; }
    public string? ContentHash { get; set; }
}

public class MarketingContentAsset : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? ContentItemId { get; set; }
    public Guid? CampaignId { get; set; }
    public string Kind { get; set; } = "Brief";
    public string? Platform { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Prompt { get; set; }
    public string? Brief { get; set; }
    public string? StorageUrl { get; set; }
    public string? StoragePath { get; set; }
    public string? MimeType { get; set; }
    public string Source { get; set; } = "BriefOnly";
    public bool BrandCompliant { get; set; }
    public string Status { get; set; } = "Ready";
}

public class MarketingResearchReport : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Kind { get; set; } = "Competitor";
    public string Title { get; set; } = default!;
    public string Body { get; set; } = "";
    public string SourcesJson { get; set; } = "[]";
    public string CompetitorNamesJson { get; set; } = "[]";
    public string ThemesJson { get; set; } = "[]";
    public string KeywordsJson { get; set; } = "[]";
    public string OpportunitiesJson { get; set; } = "[]";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string? AgentKey { get; set; }
    public bool Fabricated { get; set; }
}

public class MarketingPublishJob : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ContentItemId { get; set; }
    public string Provider { get; set; } = default!;
    public string AccountExternalId { get; set; } = "";
    public DateTime RunAtUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public int Attempt { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public string IdempotencyKey { get; set; } = default!;
    public string? LastError { get; set; }
    public string? ExternalPostId { get; set; }
    public string? ClaimToken { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class MarketingAnalyticsSnapshot : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? ContentItemId { get; set; }
    public Guid? CampaignId { get; set; }
    public string Provider { get; set; } = default!;
    public string? ExternalPostId { get; set; }
    public string? MetricsJson { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public string DataSource { get; set; } = default!;
    public string UnavailableMetricsJson { get; set; } = "[]";
}

public class MarketingAgentPolicy : AuditableEntity
{
    public Guid TenantId { get; set; }
    public bool RequireApprovalBeforePublish { get; set; } = true;
    public int DailyPublishLimit { get; set; } = 5;
    public decimal WeeklyAiBudgetUsd { get; set; } = 25m;
    public decimal MonthlyAiBudgetUsd { get; set; } = 100m;
    public string TimeZoneId { get; set; } = "UTC";
    public bool DailyCronEnabled { get; set; }
    public bool WeeklyCronEnabled { get; set; } = true;
    public bool MonthlyCronEnabled { get; set; }
    public string? PausedReason { get; set; }
    public bool IsPaused { get; set; }
}

using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class MarketingBrandProfileConfiguration : IEntityTypeConfiguration<MarketingBrandProfile>
{
    public void Configure(EntityTypeBuilder<MarketingBrandProfile> builder)
    {
        builder.ToTable("MarketingBrandProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BusinessName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Industry).HasMaxLength(120);
        builder.Property(x => x.ProductsServicesJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.TargetAudience).HasMaxLength(2000);
        builder.Property(x => x.BrandDescription).HasMaxLength(4000);
        builder.Property(x => x.BrandVoice).HasMaxLength(1000);
        builder.Property(x => x.BrandColorsJson).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.LogoUrl).HasMaxLength(1000);
        builder.Property(x => x.Website).HasMaxLength(500);
        builder.Property(x => x.SocialAccountsJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.MarketingObjectivesJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.PreferredLanguagesJson).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentRestrictions).HasMaxLength(2000);
        builder.Property(x => x.CompetitorsJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.MarketingPreferencesJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.SourceNotes).HasMaxLength(2000);
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

public class MarketingCampaignConfiguration : IEntityTypeConfiguration<MarketingCampaign>
{
    public void Configure(EntityTypeBuilder<MarketingCampaign> builder)
    {
        builder.ToTable("MarketingCampaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Objective).HasMaxLength(60).IsRequired();
        builder.Property(x => x.TargetAudience).HasMaxLength(2000);
        builder.Property(x => x.ContentThemesJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.RecommendedPlatformsJson).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.PublishingFrequency).HasMaxLength(80);
        builder.Property(x => x.ContentFormatsJson).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.SuccessMetricsJson).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.EstimatedAiCostUsd).HasPrecision(18, 6);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.StrategyBody).HasMaxLength(16000);
        builder.Property(x => x.StrategyApprovedByUserId).HasMaxLength(450);
        builder.Property(x => x.AgentKey).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class MarketingContentItemConfiguration : IEntityTypeConfiguration<MarketingContentItem>
{
    public void Configure(EntityTypeBuilder<MarketingContentItem> builder)
    {
        builder.ToTable("MarketingContentItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(16000).IsRequired();
        builder.Property(x => x.Platform).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Caption).HasMaxLength(4000);
        builder.Property(x => x.HashtagsJson).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CallToAction).HasMaxLength(500);
        builder.Property(x => x.ImageAssetIdsJson).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ApprovalStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ApprovalFingerprint).HasMaxLength(128);
        builder.Property(x => x.ApprovedByUserId).HasMaxLength(450);
        builder.Property(x => x.RejectedReason).HasMaxLength(2000);
        builder.Property(x => x.ConnectedAccountProvider).HasMaxLength(40);
        builder.Property(x => x.ConnectedAccountExternalId).HasMaxLength(200);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120);
        builder.Property(x => x.AgentKey).HasMaxLength(80);
        builder.Property(x => x.ContentHash).HasMaxLength(128);
        builder.HasIndex(x => new { x.TenantId, x.ApprovalStatus });
        builder.HasIndex(x => new { x.TenantId, x.SuggestedPublishAt });
        builder.HasIndex(x => new { x.TenantId, x.CampaignId });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}

public class MarketingContentAssetConfiguration : IEntityTypeConfiguration<MarketingContentAsset>
{
    public void Configure(EntityTypeBuilder<MarketingContentAsset> builder)
    {
        builder.ToTable("MarketingContentAssets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Platform).HasMaxLength(40);
        builder.Property(x => x.Prompt).HasMaxLength(4000);
        builder.Property(x => x.Brief).HasMaxLength(8000);
        builder.Property(x => x.StorageUrl).HasMaxLength(1000);
        builder.Property(x => x.StoragePath).HasMaxLength(1000);
        builder.Property(x => x.MimeType).HasMaxLength(120);
        builder.Property(x => x.Source).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.ContentItemId });
        builder.HasIndex(x => new { x.TenantId, x.CampaignId });
    }
}

public class MarketingResearchReportConfiguration : IEntityTypeConfiguration<MarketingResearchReport>
{
    public void Configure(EntityTypeBuilder<MarketingResearchReport> builder)
    {
        builder.ToTable("MarketingResearchReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(16000).IsRequired();
        builder.Property(x => x.SourcesJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.CompetitorNamesJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ThemesJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.KeywordsJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.OpportunitiesJson).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.AgentKey).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.Kind });
    }
}

public class MarketingPublishJobConfiguration : IEntityTypeConfiguration<MarketingPublishJob>
{
    public void Configure(EntityTypeBuilder<MarketingPublishJob> builder)
    {
        builder.ToTable("MarketingPublishJobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        builder.Property(x => x.AccountExternalId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        builder.Property(x => x.ClaimToken).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.RunAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ContentItemId });
    }
}

public class MarketingAnalyticsSnapshotConfiguration : IEntityTypeConfiguration<MarketingAnalyticsSnapshot>
{
    public void Configure(EntityTypeBuilder<MarketingAnalyticsSnapshot> builder)
    {
        builder.ToTable("MarketingAnalyticsSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ExternalPostId).HasMaxLength(200);
        // Avoid HasMaxLength(8000): SQL Server nvarchar parameters cap at 4000 for non-max typed params.
        builder.Property(x => x.MetricsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.DataSource).HasMaxLength(80).IsRequired();
        builder.Property(x => x.UnavailableMetricsJson).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.ContentItemId });
        builder.HasIndex(x => new { x.TenantId, x.CampaignId });
        builder.HasIndex(x => new { x.TenantId, x.SyncedAt });
    }
}

public class MarketingAgentPolicyConfiguration : IEntityTypeConfiguration<MarketingAgentPolicy>
{
    public void Configure(EntityTypeBuilder<MarketingAgentPolicy> builder)
    {
        builder.ToTable("MarketingAgentPolicies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WeeklyAiBudgetUsd).HasPrecision(18, 6);
        builder.Property(x => x.MonthlyAiBudgetUsd).HasPrecision(18, 6);
        builder.Property(x => x.TimeZoneId).HasMaxLength(80).IsRequired();
        builder.Property(x => x.PausedReason).HasMaxLength(1000);
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

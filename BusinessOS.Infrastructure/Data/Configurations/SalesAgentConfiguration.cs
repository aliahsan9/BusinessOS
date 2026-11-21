using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class SalesAgentSettingsConfiguration : IEntityTypeConfiguration<SalesAgentSettings>
{
    public void Configure(EntityTypeBuilder<SalesAgentSettings> builder)
    {
        builder.ToTable("SalesAgentSettings");
        builder.Property(x => x.ResearchProviderKey).HasMaxLength(80);
        builder.Property(x => x.ResearchBaseUrl).HasMaxLength(500);
        builder.Property(x => x.CurrentObjective).HasMaxLength(2000);
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

public class SalesAgentObjectiveConfiguration : IEntityTypeConfiguration<SalesAgentObjective>
{
    public void Configure(EntityTypeBuilder<SalesAgentObjective> builder)
    {
        builder.ToTable("SalesAgentObjectives");
        builder.Property(x => x.ObjectiveText).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.DelegatedByAgentKey).HasMaxLength(80);
        builder.Property(x => x.AssignedByUserId).HasMaxLength(450);
        builder.Property(x => x.ResultSummary).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }
}

public class SalesProspectResearchConfiguration : IEntityTypeConfiguration<SalesProspectResearch>
{
    public void Configure(EntityTypeBuilder<SalesProspectResearch> builder)
    {
        builder.ToTable("SalesProspectResearch");
        builder.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Website).HasMaxLength(400);
        builder.Property(x => x.Industry).HasMaxLength(120);
        builder.Property(x => x.Country).HasMaxLength(120);
        builder.Property(x => x.CompanySize).HasMaxLength(80);
        builder.Property(x => x.PublicContactEmail).HasMaxLength(320);
        builder.Property(x => x.PublicContactPhone).HasMaxLength(80);
        builder.Property(x => x.DecisionMakerName).HasMaxLength(200);
        builder.Property(x => x.DecisionMakerTitle).HasMaxLength(200);
        builder.Property(x => x.SourceUrl).HasMaxLength(500);
        builder.Property(x => x.ProviderKey).HasMaxLength(80);
        builder.Property(x => x.SourceAttribution).HasMaxLength(500);
        builder.Property(x => x.DedupKey).HasMaxLength(128);
        builder.Property(x => x.QualityStatus).HasMaxLength(40);
        builder.Property(x => x.ScoreExplanation).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.DedupKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ResearchedAt });
        builder.HasIndex(x => new { x.TenantId, x.IsQualified });
    }
}

public class SalesOutreachCampaignConfiguration : IEntityTypeConfiguration<SalesOutreachCampaign>
{
    public void Configure(EntityTypeBuilder<SalesOutreachCampaign> builder)
    {
        builder.ToTable("SalesOutreachCampaigns");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }
}

public class SalesOutreachMessageConfiguration : IEntityTypeConfiguration<SalesOutreachMessage>
{
    public void Configure(EntityTypeBuilder<SalesOutreachMessage> builder)
    {
        builder.ToTable("SalesOutreachMessages");
        builder.Property(x => x.ToEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.FitExplanation).HasMaxLength(2000);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.ResponseSummary).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.CampaignId });
    }
}

public class SalesEmailSuppressionConfiguration : IEntityTypeConfiguration<SalesEmailSuppression>
{
    public void Configure(EntityTypeBuilder<SalesEmailSuppression> builder)
    {
        builder.ToTable("SalesEmailSuppressions");
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(120);
        builder.Property(x => x.Source).HasMaxLength(120);
        builder.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
    }
}

public class SalesOutboundEmailLogConfiguration : IEntityTypeConfiguration<SalesOutboundEmailLog>
{
    public void Configure(EntityTypeBuilder<SalesOutboundEmailLog> builder)
    {
        builder.ToTable("SalesOutboundEmailLogs");
        builder.Property(x => x.ToEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(300);
        builder.Property(x => x.ProviderKey).HasMaxLength(80);
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.Error).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.OutreachMessageId });
    }
}

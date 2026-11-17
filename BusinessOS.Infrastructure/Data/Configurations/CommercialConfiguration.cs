using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class PlanFeatureGrantConfiguration : IEntityTypeConfiguration<PlanFeatureGrant>
{
    public void Configure(EntityTypeBuilder<PlanFeatureGrant> builder)
    {
        builder.ToTable("PlanFeatureGrants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeatureKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.SubscriptionPlanId, x.FeatureKey, x.EffectiveFrom });
        builder.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CompanyFeatureOverrideConfiguration : IEntityTypeConfiguration<CompanyFeatureOverride>
{
    public void Configure(EntityTypeBuilder<CompanyFeatureOverride> builder)
    {
        builder.ToTable("CompanyFeatureOverrides");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeatureKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.FeatureKey, x.EffectiveFrom });
        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AiModelPriceConfiguration : IEntityTypeConfiguration<AiModelPrice>
{
    public void Configure(EntityTypeBuilder<AiModelPrice> builder)
    {
        builder.ToTable("AiModelPrices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(80).IsRequired();
        builder.Property(x => x.UsageCategory).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PricingVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.InputUsdPerMillion).HasPrecision(18, 6);
        builder.Property(x => x.OutputUsdPerMillion).HasPrecision(18, 6);
        builder.HasIndex(x => new { x.Model, x.IsActive, x.EffectiveFrom });
    }
}

public class UsageAlertConfiguration : IEntityTypeConfiguration<UsageAlert>
{
    public void Configure(EntityTypeBuilder<UsageAlert> builder)
    {
        builder.ToTable("UsageAlerts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Resource).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Level).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Resource, x.Level, x.PeriodStart }).IsUnique();
        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class CompanyStorefrontConfiguration : IEntityTypeConfiguration<CompanyStorefront>
{
    public void Configure(EntityTypeBuilder<CompanyStorefront> builder)
    {
        builder.ToTable("CompanyStorefronts");
        builder.Property(x => x.Tagline).HasMaxLength(180);
        builder.Property(x => x.PublicDescription).HasMaxLength(4000);
        builder.Property(x => x.CoverImageUrl).HasMaxLength(500);
        builder.Property(x => x.PublicEmail).HasMaxLength(200);
        builder.Property(x => x.PublicPhone).HasMaxLength(40);
        builder.Property(x => x.Website).HasMaxLength(300);
        builder.Property(x => x.Location).HasMaxLength(240);
        builder.Property(x => x.Industry).HasMaxLength(120);
        builder.Property(x => x.Category).HasMaxLength(120);
        builder.Property(x => x.SocialLinksJson).HasMaxLength(2000);
        builder.Property(x => x.HoursJson).HasMaxLength(2000);
        builder.Property(x => x.BrandColor).HasMaxLength(7);
        builder.Property(x => x.Layout).HasMaxLength(40);
        builder.Property(x => x.SeoTitle).HasMaxLength(160);
        builder.Property(x => x.SeoDescription).HasMaxLength(320);
        builder.Property(x => x.ConsentNotice).HasMaxLength(1000);
        builder.Property(x => x.PreferredWorkspace).HasMaxLength(40);
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

public class StorefrontSlugRedirectConfiguration : IEntityTypeConfiguration<StorefrontSlugRedirect>
{
    public void Configure(EntityTypeBuilder<StorefrontSlugRedirect> builder)
    {
        builder.ToTable("StorefrontSlugRedirects");
        builder.Property(x => x.FromSlug).HasMaxLength(80);
        builder.HasIndex(x => x.FromSlug).IsUnique();
    }
}

public class StorefrontOfferingConfiguration : IEntityTypeConfiguration<StorefrontOffering>
{
    public void Configure(EntityTypeBuilder<StorefrontOffering> builder)
    {
        builder.ToTable("StorefrontOfferings");
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Summary).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Category).HasMaxLength(120);
        builder.Property(x => x.Sku).HasMaxLength(80);
        builder.Property(x => x.PricingModel).HasMaxLength(20);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Deliverables).HasMaxLength(2000);
        builder.Property(x => x.DeliveryTime).HasMaxLength(120);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.IsPublished });
    }
}

public class StorefrontPackageConfiguration : IEntityTypeConfiguration<StorefrontPackage>
{
    public void Configure(EntityTypeBuilder<StorefrontPackage> builder)
    {
        builder.ToTable("StorefrontPackages");
        builder.Property(x => x.Name).HasMaxLength(160);
        builder.Property(x => x.Scope).HasMaxLength(2000);
        builder.Property(x => x.Deliverables).HasMaxLength(2000);
        builder.Property(x => x.PricingModel).HasMaxLength(20);
        builder.Property(x => x.Price).HasPrecision(18, 2);
    }
}

public class StorefrontPortfolioItemConfiguration : IEntityTypeConfiguration<StorefrontPortfolioItem>
{
    public void Configure(EntityTypeBuilder<StorefrontPortfolioItem> builder)
    {
        builder.ToTable("StorefrontPortfolioItems");
        builder.Property(x => x.Title).HasMaxLength(180);
        builder.Property(x => x.Summary).HasMaxLength(2000);
        builder.Property(x => x.CoverImageUrl).HasMaxLength(500);
        builder.Property(x => x.Technologies).HasMaxLength(400);
        builder.Property(x => x.Industry).HasMaxLength(120);
        builder.Property(x => x.ProjectUrl).HasMaxLength(300);
        builder.Property(x => x.Results).HasMaxLength(1000);
    }
}

public class StorefrontReviewConfiguration : IEntityTypeConfiguration<StorefrontReview>
{
    public void Configure(EntityTypeBuilder<StorefrontReview> builder)
    {
        builder.ToTable("StorefrontReviews");
        builder.Property(x => x.DisplayName).HasMaxLength(80);
        builder.Property(x => x.Body).HasMaxLength(2000);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.ReportReason).HasMaxLength(400);
    }
}

public class StorefrontInquiryConfiguration : IEntityTypeConfiguration<StorefrontInquiry>
{
    public void Configure(EntityTypeBuilder<StorefrontInquiry> builder)
    {
        builder.ToTable("StorefrontInquiries");
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Phone).HasMaxLength(40);
        builder.Property(x => x.BudgetRange).HasMaxLength(80);
        builder.Property(x => x.Requirements).HasMaxLength(4000);
        builder.Property(x => x.Message).HasMaxLength(4000);
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.ClientToken).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.ClientToken });
    }
}

public class DashboardPreferenceConfiguration : IEntityTypeConfiguration<DashboardPreference>
{
    public void Configure(EntityTypeBuilder<DashboardPreference> builder)
    {
        builder.ToTable("DashboardPreferences");
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.WidgetsJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
    }
}

public class SupplierPortalAccessConfiguration : IEntityTypeConfiguration<SupplierPortalAccess>
{
    public void Configure(EntityTypeBuilder<SupplierPortalAccess> builder)
    {
        builder.ToTable("SupplierPortalAccesses");
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.TokenHash).HasMaxLength(128);
        builder.HasIndex(x => x.TokenHash).IsUnique();
    }
}

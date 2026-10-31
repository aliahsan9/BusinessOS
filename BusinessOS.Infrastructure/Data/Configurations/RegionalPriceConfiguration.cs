using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class RegionalPriceConfiguration : IEntityTypeConfiguration<RegionalPrice>
{
    public void Configure(EntityTypeBuilder<RegionalPrice> builder)
    {
        builder.ToTable("RegionalPrices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.MonthlyAmount).HasPrecision(18, 3);
        builder.Property(x => x.AnnualAmount).HasPrecision(18, 3);
        builder.Property(x => x.ProviderPriceIdMonthly).HasMaxLength(200);
        builder.Property(x => x.ProviderPriceIdAnnual).HasMaxLength(200);
        builder.Property(x => x.ExchangeRateNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.SubscriptionPlanId, x.CountryCode, x.CurrencyCode }).IsUnique();
        builder.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

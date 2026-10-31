using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.MonthlyPrice).HasPrecision(18, 3);
        builder.Property(x => x.AnnualPrice).HasPrecision(18, 3);
        builder.Property(x => x.InventoryAccess).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SalesAccess).HasMaxLength(20).IsRequired();
        builder.Property(x => x.BaseCurrency).HasMaxLength(3).IsRequired();

        builder.HasIndex(x => x.Slug).IsUnique();
    }
}

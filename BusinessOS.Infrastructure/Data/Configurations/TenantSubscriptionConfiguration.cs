using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("TenantSubscriptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.BillingInterval).HasConversion<int>().IsRequired();
        builder.Property(x => x.PaymentProvider).HasConversion<int>().IsRequired();
        builder.Property(x => x.StripeCustomerId).HasMaxLength(200);
        builder.Property(x => x.StripeSubscriptionId).HasMaxLength(200);
        builder.Property(x => x.ExternalReference).HasMaxLength(200);
        builder.Property(x => x.ContractedMonthlyPrice).HasPrecision(18, 3);
        builder.Property(x => x.ContractedAnnualPrice).HasPrecision(18, 3);
        builder.Property(x => x.ContractedCurrency).HasMaxLength(3);
        builder.Property(x => x.ContractedLimitsJson).HasMaxLength(4000);
        builder.Property(x => x.BillingCountry).HasMaxLength(2);
        builder.Property(x => x.DisplayCurrency).HasMaxLength(3);
        builder.Property(x => x.SettlementCurrency).HasMaxLength(3);
        builder.Property(x => x.PaymentMethodSummary).HasMaxLength(120);

        builder.HasIndex(x => x.TenantId).IsUnique();

        builder.HasOne(x => x.Tenant)
            .WithOne(x => x.Subscription)
            .HasForeignKey<TenantSubscription>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Plan)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

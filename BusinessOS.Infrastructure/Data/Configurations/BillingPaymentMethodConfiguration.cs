using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class BillingPaymentMethodConfiguration : IEntityTypeConfiguration<BillingPaymentMethod>
{
    public void Configure(EntityTypeBuilder<BillingPaymentMethod> builder)
    {
        builder.ToTable("BillingPaymentMethods");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasConversion<int>();
        builder.Property(x => x.ExternalId).HasMaxLength(200);
        builder.Property(x => x.Brand).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Last4).HasMaxLength(4).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Provider, x.ExternalId });
        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

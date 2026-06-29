using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class BillingInvoiceConfiguration : IEntityTypeConfiguration<BillingInvoice>
{
    public void Configure(EntityTypeBuilder<BillingInvoice> builder)
    {
        builder.ToTable("BillingInvoices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PlanName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Subtotal).AsMoney();
        builder.Property(x => x.TaxAmount).AsMoney();
        builder.Property(x => x.TotalAmount).AsMoney();
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.BillingInterval).HasConversion<int>();
        builder.Property(x => x.PaymentProvider).HasConversion<int>();
        builder.Property(x => x.PaymentMethod).HasMaxLength(100);
        builder.Property(x => x.ExternalInvoiceId).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

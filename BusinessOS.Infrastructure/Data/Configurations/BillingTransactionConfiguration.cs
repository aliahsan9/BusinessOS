using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class BillingTransactionConfiguration : IEntityTypeConfiguration<BillingTransaction>
{
    public void Configure(EntityTypeBuilder<BillingTransaction> builder)
    {
        builder.ToTable("BillingTransactions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransactionId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Amount).AsMoney();
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Provider).HasConversion<int>();
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Metadata).HasMaxLength(4000);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.TransactionId);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.BillingInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

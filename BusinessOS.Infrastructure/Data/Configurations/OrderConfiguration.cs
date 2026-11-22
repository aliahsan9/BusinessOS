using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PaymentStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ShippingAddress).HasMaxLength(500);
        builder.Property(x => x.ExternalImportKey).HasMaxLength(100);
        builder.Property(x => x.TotalAmount).AsMoney();
        builder.Property(x => x.Discount).AsMoney();
        builder.Property(x => x.Tax).AsMoney();
        builder.Property(x => x.GrandTotal).AsMoney();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ExternalImportKey })
            .IsUnique()
            .HasFilter("[ExternalImportKey] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        // Finance/analytics filter on OrderDate; CreatedAt indexes alone cause range scans.
        builder.HasIndex(x => new { x.TenantId, x.OrderDate });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.OrderDate })
            .IsDescending(false, false, true);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt })
            .IsDescending(false, false, true);
        builder.HasIndex(x => new { x.TenantId, x.CustomerId, x.CreatedAt })
            .IsDescending(false, false, true);
        builder.HasIndex(x => x.OrderNumber);
    }
}

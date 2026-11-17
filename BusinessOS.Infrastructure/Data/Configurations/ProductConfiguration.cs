using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SKU).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CostPrice).AsMoney();
        builder.Property(x => x.SalePrice).AsMoney();
        builder.Property(x => x.CurrentStock).AsMoney();
        builder.Property(x => x.ReorderLevel).AsMoney();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(80);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(40);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.PricingModel).HasMaxLength(40);
        builder.Property(x => x.Duration).HasMaxLength(80);
        builder.Property(x => x.DeliveryDetails).HasMaxLength(500);

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.SKU }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CategoryId });
        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}

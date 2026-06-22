using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.SKU)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CostPrice).HasPrecision(18, 2);
        builder.Property(x => x.SalePrice).HasPrecision(18, 2);
        builder.Property(x => x.CurrentStock).HasPrecision(18, 2);
        builder.Property(x => x.ReorderLevel).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.TenantId, x.SKU }).IsUnique();
    }
}

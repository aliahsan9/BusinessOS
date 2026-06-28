using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CurrentStock).AsMoney();
        builder.Property(x => x.MinimumStockLevel).AsMoney();
        builder.Property(x => x.MaximumStockLevel).AsMoney();
        builder.Property(x => x.ReorderLevel).AsMoney();
        builder.Property(x => x.LastUpdated).IsRequired();

        builder.HasOne(x => x.Product)
            .WithOne(x => x.Inventory)
            .HasForeignKey<Inventory>(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => new { x.TenantId, x.ProductId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CurrentStock });
        builder.HasIndex(x => new { x.TenantId, x.ReorderLevel });
    }
}

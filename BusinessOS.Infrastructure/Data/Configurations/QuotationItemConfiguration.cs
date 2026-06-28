using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class QuotationItemConfiguration : IEntityTypeConfiguration<QuotationItem>
{
    public void Configure(EntityTypeBuilder<QuotationItem> builder)
    {
        builder.ToTable("QuotationItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).AsMoney();
        builder.Property(x => x.UnitPrice).AsMoney();
        builder.Property(x => x.Total).AsMoney();

        builder.HasOne(x => x.Quotation)
            .WithMany(q => q.QuotationItems)
            .HasForeignKey(x => x.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.QuotationId, x.ProductId });
    }
}

using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.ToTable("StockTransactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransactionType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Quantity).AsMoney();
        builder.Property(x => x.PreviousStock).AsMoney();
        builder.Property(x => x.NewStock).AsMoney();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.UserId).HasMaxLength(450);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.TransactionType);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.CreatedAt });
    }
}

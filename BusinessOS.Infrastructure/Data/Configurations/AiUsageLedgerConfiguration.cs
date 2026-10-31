using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class AiUsageLedgerConfiguration : IEntityTypeConfiguration<AiUsageLedger>
{
    public void Configure(EntityTypeBuilder<AiUsageLedger> builder)
    {
        builder.ToTable("AiUsageLedgers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OperationKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EstimatedProviderCostUsd).HasPrecision(18, 6);
        builder.HasIndex(x => new { x.TenantId, x.OperationKey }).IsUnique();
        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

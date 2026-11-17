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
        builder.Property(x => x.ActualProviderChargeUsd).HasPrecision(18, 6);
        builder.Property(x => x.Model).HasMaxLength(80);
        builder.Property(x => x.AgentKey).HasMaxLength(64);
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.UsageCategory).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.OperationKey }).IsUnique();
        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

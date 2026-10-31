using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Operation).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.State).HasConversion<int>();
        builder.Property(x => x.ResponseBody).HasMaxLength(4000);

        builder.HasIndex(x => new { x.TenantId, x.Operation, x.Key }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ExpiresAt });
    }
}

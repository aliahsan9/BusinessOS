using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class VectorSyncOutboxMessageConfiguration : IEntityTypeConfiguration<VectorSyncOutboxMessage>
{
    public void Configure(EntityTypeBuilder<VectorSyncOutboxMessage> builder)
    {
        builder.ToTable("VectorSyncOutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.Operation).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
    }
}

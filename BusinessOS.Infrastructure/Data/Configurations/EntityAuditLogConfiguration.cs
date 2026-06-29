using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class EntityAuditLogConfiguration : IEntityTypeConfiguration<EntityAuditLog>
{
    public void Configure(EntityTypeBuilder<EntityAuditLog> builder)
    {
        builder.ToTable("EntityAuditLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChangedBy).HasMaxLength(450).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OldValues).HasMaxLength(8000);
        builder.Property(x => x.NewValues).HasMaxLength(8000);

        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.ChangedAt });
        builder.HasIndex(x => new { x.TenantId, x.ChangedBy, x.ChangedAt });
    }
}

using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public sealed class RbacAuditLogConfiguration : IEntityTypeConfiguration<RbacAuditLog>
{
    public void Configure(EntityTypeBuilder<RbacAuditLog> builder)
    {
        builder.ToTable("RbacAuditLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.OldValue)
            .HasMaxLength(4000);

        builder.Property(x => x.NewValue)
            .HasMaxLength(4000);

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.ActorUserId);
    }
}

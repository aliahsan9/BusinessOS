using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class TenantAuditLogConfiguration : IEntityTypeConfiguration<TenantAuditLog>
{
    public void Configure(EntityTypeBuilder<TenantAuditLog> builder)
    {
        builder.ToTable("TenantAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorUserId).HasMaxLength(450);
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100);
        builder.Property(x => x.OldValue).HasMaxLength(4000);
        builder.Property(x => x.NewValue).HasMaxLength(4000);

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

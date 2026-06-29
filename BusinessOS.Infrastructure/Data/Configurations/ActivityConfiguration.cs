using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Metadata).HasMaxLength(4000);

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId, x.Action });
    }
}

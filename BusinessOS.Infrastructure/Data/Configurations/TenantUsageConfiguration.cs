using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class TenantUsageConfiguration : IEntityTypeConfiguration<TenantUsage>
{
    public void Configure(EntityTypeBuilder<TenantUsage> builder)
    {
        builder.ToTable("TenantUsage");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId).IsUnique();

        builder.HasOne(x => x.Tenant)
            .WithOne(x => x.Usage)
            .HasForeignKey<TenantUsage>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

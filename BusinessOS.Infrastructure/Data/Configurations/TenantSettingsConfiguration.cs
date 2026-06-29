using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("TenantSettings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Language).HasMaxLength(10).IsRequired();
        builder.Property(x => x.TaxRate).AsMoney();
        builder.Property(x => x.InvoicePrefix).HasMaxLength(20);
        builder.Property(x => x.EmailFromAddress).HasMaxLength(256);
        builder.Property(x => x.Theme).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.LogoUrl).HasMaxLength(500);
        builder.Property(x => x.Timezone).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.TenantId).IsUnique();

        builder.HasOne(x => x.Tenant)
            .WithOne(x => x.Settings)
            .HasForeignKey<TenantSettings>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

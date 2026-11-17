using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class CompanyMembershipConfiguration : IEntityTypeConfiguration<CompanyMembership>
{
    public void Configure(EntityTypeBuilder<CompanyMembership> builder)
    {
        builder.ToTable("CompanyMemberships");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.RoleName).HasMaxLength(64).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.UserId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.UserId, x.IsActive });

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BusinessTypeTemplateConfiguration : IEntityTypeConfiguration<BusinessTypeTemplate>
{
    public void Configure(EntityTypeBuilder<BusinessTypeTemplate> builder)
    {
        builder.ToTable("BusinessTypeTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ModulesJson).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}

public class PlatformAnnouncementConfiguration : IEntityTypeConfiguration<PlatformAnnouncement>
{
    public void Configure(EntityTypeBuilder<PlatformAnnouncement> builder)
    {
        builder.ToTable("PlatformAnnouncements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => new { x.IsPublished, x.IsActive, x.StartsAt });
    }
}

public class PlatformAnnouncementTargetConfiguration : IEntityTypeConfiguration<PlatformAnnouncementTarget>
{
    public void Configure(EntityTypeBuilder<PlatformAnnouncementTarget> builder)
    {
        builder.ToTable("PlatformAnnouncementTargets");
        builder.HasKey(x => new { x.AnnouncementId, x.TargetTenantId });

        builder.HasOne(x => x.Announcement)
            .WithMany(x => x.Targets)
            .HasForeignKey(x => x.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TargetTenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> builder)
    {
        builder.ToTable("PlatformSettings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(80);
        builder.Property(x => x.Value).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
    }
}

public class PlatformAuditLogConfiguration : IEntityTypeConfiguration<PlatformAuditLog>
{
    public void Configure(EntityTypeBuilder<PlatformAuditLog> builder)
    {
        builder.ToTable("PlatformAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100);
        builder.Property(x => x.EntityId).HasMaxLength(64);
        builder.Property(x => x.MetadataJson).HasMaxLength(2000);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.SubjectTenantId);
    }
}

public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("StoredFiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OwnerUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

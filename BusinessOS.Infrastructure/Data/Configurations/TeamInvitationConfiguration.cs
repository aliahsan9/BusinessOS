using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class TeamInvitationConfiguration : IEntityTypeConfiguration<TeamInvitation>
{
    public void Configure(EntityTypeBuilder<TeamInvitation> builder)
    {
        builder.ToTable("TeamInvitations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Token).HasMaxLength(128).IsRequired();
        builder.Property(x => x.InvitedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Email, x.Status });
    }
}

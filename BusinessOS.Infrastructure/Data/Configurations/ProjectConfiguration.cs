using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Tags).HasMaxLength(500);
        builder.Property(x => x.AssignedUserId).HasMaxLength(450);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(40);
        builder.Property(x => x.ManagerUserId).HasMaxLength(450);
        builder.Property(x => x.Budget).AsMoney();
        builder.Property(x => x.Visibility).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Workflow).HasMaxLength(40).IsRequired();

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => x.AssignedUserId);
    }
}

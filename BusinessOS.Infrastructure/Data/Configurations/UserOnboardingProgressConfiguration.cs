using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class UserOnboardingProgressConfiguration : IEntityTypeConfiguration<UserOnboardingProgress>
{
    public void Configure(EntityTypeBuilder<UserOnboardingProgress> builder)
    {
        builder.ToTable("UserOnboardingProgress");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique();
    }
}

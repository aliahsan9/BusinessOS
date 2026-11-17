using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Designation).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Salary).AsMoney();
        builder.Property(x => x.EmployeeCode).HasMaxLength(40);
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.ProfilePictureUrl).HasMaxLength(500);
        builder.Property(x => x.EmploymentStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EmploymentType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Skills).HasMaxLength(1000);
        builder.Property(x => x.WorkLocation).HasMaxLength(200);
        builder.Property(x => x.EmergencyContactName).HasMaxLength(120);
        builder.Property(x => x.EmergencyContactPhone).HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.Email });
        builder.HasIndex(x => new { x.TenantId, x.EmployeeCode }).IsUnique().HasFilter("[EmployeeCode] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

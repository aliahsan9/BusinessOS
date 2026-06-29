using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class GeneratedReportConfiguration : IEntityTypeConfiguration<GeneratedReport>
{
    public void Configure(EntityTypeBuilder<GeneratedReport> builder)
    {
        builder.ToTable("GeneratedReports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReportName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ReportType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FileType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.GeneratedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.GeneratedByName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ParametersJson).HasMaxLength(4000);

        builder.HasIndex(x => new { x.TenantId, x.GeneratedAt });
        builder.HasIndex(x => new { x.TenantId, x.ReportType });
    }
}

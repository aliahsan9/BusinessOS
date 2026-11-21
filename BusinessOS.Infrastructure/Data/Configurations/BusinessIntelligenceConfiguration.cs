using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class PredictionAccuracyLogConfiguration : IEntityTypeConfiguration<PredictionAccuracyLog>
{
    public void Configure(EntityTypeBuilder<PredictionAccuracyLog> builder)
    {
        builder.ToTable("PredictionAccuracyLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MetricKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Method).HasMaxLength(80).IsRequired();
        builder.Property(x => x.PredictedValue).HasPrecision(18, 4);
        builder.Property(x => x.ActualValue).HasPrecision(18, 4);
        builder.Property(x => x.AbsolutePercentError).HasPrecision(18, 4);
        builder.Property(x => x.Confidence).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.AssumptionsJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TenantId, x.MetricKey, x.TargetPeriodStart });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.TargetPeriodEnd });
    }
}

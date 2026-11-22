using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class BusinessAutopilotConfigConfiguration : IEntityTypeConfiguration<BusinessAutopilotConfig>
{
    public void Configure(EntityTypeBuilder<BusinessAutopilotConfig> builder)
    {
        builder.ToTable("BusinessAutopilotConfigs");
        builder.Property(x => x.Mode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(80);
        builder.Property(x => x.LastActivatedByUserId).HasMaxLength(450);
        builder.Property(x => x.MonthlySalesTarget).HasPrecision(18, 2);
        builder.Property(x => x.MarketingBudgetMonthly).HasPrecision(18, 2);
        builder.Property(x => x.SpendingLimitPerAction).HasPrecision(18, 2);
        builder.Property(x => x.DailySpendingLimit).HasPrecision(18, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

public class BusinessGoalConfiguration : IEntityTypeConfiguration<BusinessGoal>
{
    public void Configure(EntityTypeBuilder<BusinessGoal> builder)
    {
        builder.ToTable("BusinessGoals");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MetricKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(40);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.TargetValue).HasPrecision(18, 4);
        builder.Property(x => x.BaselineValue).HasPrecision(18, 4);
        builder.Property(x => x.PredictedValue).HasPrecision(18, 4);
        builder.Property(x => x.ActualValue).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.PeriodEndUtc });
    }
}

public class BusinessGoalProgressConfiguration : IEntityTypeConfiguration<BusinessGoalProgress>
{
    public void Configure(EntityTypeBuilder<BusinessGoalProgress> builder)
    {
        builder.ToTable("BusinessGoalProgress");
        builder.Property(x => x.Source).HasMaxLength(40);
        builder.Property(x => x.TargetValue).HasPrecision(18, 4);
        builder.Property(x => x.PredictedValue).HasPrecision(18, 4);
        builder.Property(x => x.ActualValue).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.TenantId, x.GoalId, x.MeasuredAtUtc });
    }
}

public class BusinessMemoryEntryConfiguration : IEntityTypeConfiguration<BusinessMemoryEntry>
{
    public void Configure(EntityTypeBuilder<BusinessMemoryEntry> builder)
    {
        builder.ToTable("BusinessMemoryEntries");
        builder.Property(x => x.Kind).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SourceAttribution).HasMaxLength(400).IsRequired();
        builder.Property(x => x.SourceEntityType).HasMaxLength(80);
        builder.Property(x => x.VectorPointId).HasMaxLength(80);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.Kind, x.CreatedAt });
    }
}

public class AutopilotOperationLogConfiguration : IEntityTypeConfiguration<AutopilotOperationLog>
{
    public void Configure(EntityTypeBuilder<AutopilotOperationLog> builder)
    {
        builder.ToTable("AutopilotOperationLogs");
        builder.Property(x => x.Kind).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.ScenarioKey).HasMaxLength(120);
        builder.Property(x => x.AgentKey).HasMaxLength(80);
        builder.Property(x => x.ToolName).HasMaxLength(80);
        builder.Property(x => x.Summary).HasMaxLength(1000);
        builder.Property(x => x.CorrelationId).HasMaxLength(120);
        builder.Property(x => x.EstimatedCostUsd).HasPrecision(18, 6);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.Kind, x.Status });
    }
}

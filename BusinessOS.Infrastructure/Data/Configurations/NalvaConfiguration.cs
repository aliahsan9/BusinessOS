using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class NalvaCompanyAgentConfiguration : IEntityTypeConfiguration<NalvaCompanyAgent>
{
    public void Configure(EntityTypeBuilder<NalvaCompanyAgent> builder)
    {
        builder.ToTable("NalvaCompanyAgents");
        builder.Property(x => x.AgentKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.RoleTitle).HasMaxLength(200);
        builder.Property(x => x.Category).HasMaxLength(80);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.Model).HasMaxLength(80);
        builder.Property(x => x.Temperature).HasPrecision(4, 2);
        builder.Property(x => x.ActivatedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.AgentKey }).IsUnique();
    }
}

public class NalvaAgentRevisionConfiguration : IEntityTypeConfiguration<NalvaAgentRevision>
{
    public void Configure(EntityTypeBuilder<NalvaAgentRevision> builder)
    {
        builder.ToTable("NalvaAgentRevisions");
        builder.Property(x => x.SnapshotJson).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CompanyAgentId, x.Version });
    }
}

public class NalvaTaskConfiguration : IEntityTypeConfiguration<NalvaTask>
{
    public void Configure(EntityTypeBuilder<NalvaTask> builder)
    {
        builder.ToTable("NalvaTasks");
        builder.Property(x => x.AgentKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Objective).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.DelegationChain).HasMaxLength(500);
        builder.Property(x => x.InitiatorUserId).HasMaxLength(450);
        builder.Property(x => x.ErrorClass).HasMaxLength(32);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.RunAfter });
    }
}

public class NalvaExecutionConfiguration : IEntityTypeConfiguration<NalvaExecution>
{
    public void Configure(EntityTypeBuilder<NalvaExecution> builder)
    {
        builder.ToTable("NalvaExecutions");
        builder.Property(x => x.AgentKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Objective).HasMaxLength(2000);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.Model).HasMaxLength(80);
        builder.Property(x => x.Temperature).HasPrecision(4, 2);
        builder.Property(x => x.EstimatedCostUsd).HasPrecision(18, 6);
        builder.Property(x => x.PricingVersion).HasMaxLength(32);
        builder.Property(x => x.DelegationChain).HasMaxLength(500);
        builder.Property(x => x.InitiatorUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.AgentKey, x.StartedAt });
    }
}

public class NalvaApprovalConfiguration : IEntityTypeConfiguration<NalvaApproval>
{
    public void Configure(EntityTypeBuilder<NalvaApproval> builder)
    {
        builder.ToTable("NalvaApprovals");
        builder.Property(x => x.AgentKey).HasMaxLength(80);
        builder.Property(x => x.Action).HasMaxLength(120);
        builder.Property(x => x.ToolName).HasMaxLength(80);
        builder.Property(x => x.Risk).HasMaxLength(32);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.RequestedByUserId).HasMaxLength(450);
        builder.Property(x => x.ApproverUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.RequestedAt });
    }
}

public class NalvaMemoryRecordConfiguration : IEntityTypeConfiguration<NalvaMemoryRecord>
{
    public void Configure(EntityTypeBuilder<NalvaMemoryRecord> builder)
    {
        builder.ToTable("NalvaMemoryRecords");
        builder.Property(x => x.AgentKey).HasMaxLength(80);
        builder.Property(x => x.Kind).HasMaxLength(32);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Content).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.AgentKey, x.Kind });
    }
}

public class NalvaKnowledgeSourceConfiguration : IEntityTypeConfiguration<NalvaKnowledgeSource>
{
    public void Configure(EntityTypeBuilder<NalvaKnowledgeSource> builder)
    {
        builder.ToTable("NalvaKnowledgeSources");
        builder.Property(x => x.AgentKey).HasMaxLength(80);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.AgentKey });
    }
}

public class NalvaWorkflowDefinitionConfiguration : IEntityTypeConfiguration<NalvaWorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<NalvaWorkflowDefinition> builder)
    {
        builder.ToTable("NalvaWorkflowDefinitions");
        builder.Property(x => x.Key).HasMaxLength(80);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.TriggerKind).HasMaxLength(32);
        builder.Property(x => x.TriggerEvent).HasMaxLength(80);
        builder.Property(x => x.LastScheduleWindow).HasMaxLength(80);
        builder.Property(x => x.TimeZoneId).HasMaxLength(80);
        builder.Property(x => x.RecurrenceKind).HasMaxLength(32).HasDefaultValue("Interval");
        builder.Property(x => x.LocalTimeOfDay).HasMaxLength(8);
        builder.Property(x => x.DaysOfWeek).HasMaxLength(64);
        builder.Property(x => x.LastFailure).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.TriggerKind, x.IsActive, x.IsPaused, x.NextRunAt });
    }
}

public class NalvaWorkflowRunConfiguration : IEntityTypeConfiguration<NalvaWorkflowRun>
{
    public void Configure(EntityTypeBuilder<NalvaWorkflowRun> builder)
    {
        builder.ToTable("NalvaWorkflowRuns");
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.TriggerSource).HasMaxLength(32);
        builder.Property(x => x.CorrelationId).HasMaxLength(120);
        builder.Property(x => x.EstimatedCostUsd).HasPrecision(18, 6);
        builder.HasIndex(x => new { x.TenantId, x.DefinitionId, x.StartedAt });
        builder.HasIndex(x => new { x.TenantId, x.CorrelationId });
    }
}

public class NalvaCompanySettingsConfiguration : IEntityTypeConfiguration<NalvaCompanySettings>
{
    public void Configure(EntityTypeBuilder<NalvaCompanySettings> builder)
    {
        builder.ToTable("NalvaCompanySettings");
        builder.Property(x => x.DefaultModel).HasMaxLength(80);
        builder.Property(x => x.EmbeddingModel).HasMaxLength(80);
        builder.Property(x => x.Temperature).HasPrecision(4, 2);
        builder.Property(x => x.MonthlyBudgetUsd).HasPrecision(18, 2);
        builder.Property(x => x.ExtraApprovalToolsJson).HasMaxLength(4000);
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

public class NalvaWorkItemConfiguration : IEntityTypeConfiguration<NalvaWorkItem>
{
    public void Configure(EntityTypeBuilder<NalvaWorkItem> builder)
    {
        builder.ToTable("NalvaWorkItems");
        builder.Property(x => x.AgentKey).HasMaxLength(80);
        builder.Property(x => x.Kind).HasMaxLength(40);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.AgentKey, x.Kind });
    }
}

public class NalvaBusinessEventConfiguration : IEntityTypeConfiguration<NalvaBusinessEvent>
{
    public void Configure(EntityTypeBuilder<NalvaBusinessEvent> builder)
    {
        builder.ToTable("NalvaBusinessEvents");
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.CorrelationId).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.OccurredAt });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey });
    }
}

public class CompanyIntegrationConnectionConfiguration : IEntityTypeConfiguration<CompanyIntegrationConnection>
{
    public void Configure(EntityTypeBuilder<CompanyIntegrationConnection> builder)
    {
        builder.ToTable("CompanyIntegrationConnections");
        builder.Property(x => x.ProviderKey).HasMaxLength(40);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.ExternalAccountId).HasMaxLength(200);
        builder.Property(x => x.ExternalAccountLabel).HasMaxLength(200);
        builder.Property(x => x.OwnerUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.ProviderKey }).IsUnique();
    }
}

public class IntegrationOAuthStateConfiguration : IEntityTypeConfiguration<IntegrationOAuthState>
{
    public void Configure(EntityTypeBuilder<IntegrationOAuthState> builder)
    {
        builder.ToTable("IntegrationOAuthStates");
        builder.Property(x => x.ProviderKey).HasMaxLength(40);
        builder.Property(x => x.State).HasMaxLength(128);
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.State });
    }
}

public class IntegrationActivityLogConfiguration : IEntityTypeConfiguration<IntegrationActivityLog>
{
    public void Configure(EntityTypeBuilder<IntegrationActivityLog> builder)
    {
        builder.ToTable("IntegrationActivityLogs");
        builder.Property(x => x.ProviderKey).HasMaxLength(40);
        builder.Property(x => x.Operation).HasMaxLength(80);
        builder.Property(x => x.CorrelationId).HasMaxLength(80);
        builder.HasIndex(x => new { x.TenantId, x.ProviderKey, x.CreatedAt });
    }
}

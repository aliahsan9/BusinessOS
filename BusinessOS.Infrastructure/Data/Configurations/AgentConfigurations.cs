using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class AgentProfileConfiguration : IEntityTypeConfiguration<AgentProfile>
{
    public void Configure(EntityTypeBuilder<AgentProfile> builder)
    {
        builder.ToTable("AgentProfiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RoleTitle).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Specialty).HasMaxLength(300).IsRequired();
        builder.Property(x => x.SystemPersonaPrompt).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.DefaultLanguage).HasMaxLength(10).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsDefault, x.IsActive });
    }
}

public class VoicePreferenceConfiguration : IEntityTypeConfiguration<VoicePreference>
{
    public void Configure(EntityTypeBuilder<VoicePreference> builder)
    {
        builder.ToTable("VoicePreferences");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.VoiceName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PreferredAgentKey).HasMaxLength(50);

        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
    }
}

public class AgentWorkflowRunConfiguration : IEntityTypeConfiguration<AgentWorkflowRun>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowRun> builder)
    {
        builder.ToTable("AgentWorkflowRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.AgentKey).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ProgressJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ResultSummary).HasMaxLength(2000);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.HasMany(x => x.Steps)
            .WithOne(x => x.WorkflowRun)
            .HasForeignKey(x => x.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.StartedAt });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.StartedAt });
    }
}

public class AgentWorkflowStepConfiguration : IEntityTypeConfiguration<AgentWorkflowStep>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowStep> builder)
    {
        builder.ToTable("AgentWorkflowSteps");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StepKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000);

        builder.HasIndex(x => new { x.WorkflowRunId, x.SortOrder });
        builder.HasIndex(x => new { x.WorkflowRunId, x.StepKey }).IsUnique();
    }
}

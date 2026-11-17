using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class AgencyWorkspaceConfiguration : IEntityTypeConfiguration<AgencyWorkspace>
{
    public void Configure(EntityTypeBuilder<AgencyWorkspace> builder)
    {
        builder.ToTable("AgencyWorkspaces");
        builder.Property(x => x.TemplateKey).HasMaxLength(64);
        builder.Property(x => x.Services).HasMaxLength(2000);
        builder.Property(x => x.Technologies).HasMaxLength(2000);
        builder.Property(x => x.TeamSize).HasMaxLength(64);
        builder.Property(x => x.TargetClients).HasMaxLength(2000);
        builder.Property(x => x.Goals).HasMaxLength(4000);
        builder.Property(x => x.ModulesJson).HasMaxLength(2000);
        builder.Property(x => x.InvitedEmailsJson).HasMaxLength(4000);
        builder.Property(x => x.RolesJson).HasMaxLength(1000);
        builder.Property(x => x.InitialProjectName).HasMaxLength(200);
        builder.Property(x => x.AgentsJson).HasMaxLength(2000);
        builder.Property(x => x.ApprovalPreference).HasMaxLength(64);
        builder.Property(x => x.MonthlyAiBudgetUsd).HasPrecision(12, 2);
        builder.Property(x => x.SelectedPlanKey).HasMaxLength(32);
        builder.Property(x => x.FirstObjective).HasMaxLength(4000);
        builder.Property(x => x.InviteNote).HasMaxLength(500);
        builder.HasIndex(x => x.TenantId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class EarlyAccessInvitationConfiguration : IEntityTypeConfiguration<EarlyAccessInvitation>
{
    public void Configure(EntityTypeBuilder<EarlyAccessInvitation> builder)
    {
        builder.ToTable("EarlyAccessInvitations");
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.AgencyName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}

public class LaunchFeedbackConfiguration : IEntityTypeConfiguration<LaunchFeedback>
{
    public void Configure(EntityTypeBuilder<LaunchFeedback> builder)
    {
        builder.ToTable("LaunchFeedback");
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Page).HasMaxLength(200);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Kind, x.CreatedAt });
    }
}

public class ProductUsageEventConfiguration : IEntityTypeConfiguration<ProductUsageEvent>
{
    public void Configure(EntityTypeBuilder<ProductUsageEvent> builder)
    {
        builder.ToTable("ProductUsageEvents");
        builder.Property(x => x.EventKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.EventKey, x.OccurredAt });
    }
}

public class ProductBacklogItemConfiguration : IEntityTypeConfiguration<ProductBacklogItem>
{
    public void Configure(EntityTypeBuilder<ProductBacklogItem> builder)
    {
        builder.ToTable("ProductBacklogItems");
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.Priority, x.Status });
    }
}

public class CompanyDataRequestConfiguration : IEntityTypeConfiguration<CompanyDataRequest>
{
    public void Configure(EntityTypeBuilder<CompanyDataRequest> builder)
    {
        builder.ToTable("CompanyDataRequests");
        builder.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Kind, x.Status });
    }
}

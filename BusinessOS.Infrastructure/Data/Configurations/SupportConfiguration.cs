using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("SupportTickets");
        builder.Property(x => x.TicketNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80);
        builder.Property(x => x.Priority).HasMaxLength(32);
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.Channel).HasMaxLength(40);
        builder.Property(x => x.AssignedAgentKey).HasMaxLength(80);
        builder.Property(x => x.AssignedUserId).HasMaxLength(450);
        builder.Property(x => x.ExternalThreadId).HasMaxLength(200);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160);
        builder.Property(x => x.AiSummary).HasMaxLength(2000);
        builder.Property(x => x.RecommendedAction).HasMaxLength(1000);
        builder.Property(x => x.EscalationReason).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.TicketNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.Priority });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey });
        builder.HasIndex(x => new { x.TenantId, x.ExternalThreadId });
        builder.HasMany(x => x.Messages).WithOne(x => x.Ticket).HasForeignKey(x => x.TicketId);
        builder.HasMany(x => x.FollowUps).WithOne(x => x.Ticket).HasForeignKey(x => x.TicketId);
    }
}

public class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessage>
{
    public void Configure(EntityTypeBuilder<SupportMessage> builder)
    {
        builder.ToTable("SupportMessages");
        builder.Property(x => x.Channel).HasMaxLength(40);
        builder.Property(x => x.Direction).HasMaxLength(20);
        builder.Property(x => x.AuthorType).HasMaxLength(40);
        builder.Property(x => x.AuthorId).HasMaxLength(450);
        builder.Property(x => x.DeliveryStatus).HasMaxLength(32);
        builder.Property(x => x.ExternalMessageId).HasMaxLength(200);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160);
        builder.HasIndex(x => new { x.TenantId, x.TicketId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey });
        builder.HasIndex(x => new { x.TenantId, x.ExternalMessageId });
    }
}

public class SupportFollowUpJobConfiguration : IEntityTypeConfiguration<SupportFollowUpJob>
{
    public void Configure(EntityTypeBuilder<SupportFollowUpJob> builder)
    {
        builder.ToTable("SupportFollowUpJobs");
        builder.Property(x => x.Kind).HasMaxLength(80);
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.Channel).HasMaxLength(40);
        builder.Property(x => x.DedupKey).HasMaxLength(160);
        builder.HasIndex(x => new { x.TenantId, x.Status, x.DueAt });
        builder.HasIndex(x => new { x.TenantId, x.DedupKey });
    }
}

public class SupportChannelAccountConfiguration : IEntityTypeConfiguration<SupportChannelAccount>
{
    public void Configure(EntityTypeBuilder<SupportChannelAccount> builder)
    {
        builder.ToTable("SupportChannelAccounts");
        builder.Property(x => x.Channel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32);
        builder.Property(x => x.DisplayName).HasMaxLength(120);
        builder.Property(x => x.ExternalAccountId).HasMaxLength(200);
        builder.HasIndex(x => new { x.TenantId, x.Channel }).IsUnique();
    }
}

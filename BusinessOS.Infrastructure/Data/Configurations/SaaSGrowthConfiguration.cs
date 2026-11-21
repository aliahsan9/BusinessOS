using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class MarketingLeadConfiguration : IEntityTypeConfiguration<MarketingLead>
{
    public void Configure(EntityTypeBuilder<MarketingLead> builder)
    {
        builder.ToTable("MarketingLeads");
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Company).HasMaxLength(200);
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Topic).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.SourcePath).HasMaxLength(200);
        builder.Property(x => x.ReferralCode).HasMaxLength(32);
        builder.Property(x => x.AssignedToUserId).HasMaxLength(450);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.Kind);
    }
}

public class PlatformSupportTicketConfiguration : IEntityTypeConfiguration<PlatformSupportTicket>
{
    public void Configure(EntityTypeBuilder<PlatformSupportTicket> builder)
    {
        builder.ToTable("PlatformSupportTickets");
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.RequesterName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RequesterEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(16).IsRequired();
        builder.Property(x => x.AgentReply).HasMaxLength(4000);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.RequesterEmail);
        builder.HasIndex(x => x.TenantId);
    }
}

public class ReferralCodeConfiguration : IEntityTypeConfiguration<ReferralCode>
{
    public void Configure(EntityTypeBuilder<ReferralCode> builder)
    {
        builder.ToTable("ReferralCodes");
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.TenantId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ReferralRedemptionConfiguration : IEntityTypeConfiguration<ReferralRedemption>
{
    public void Configure(EntityTypeBuilder<ReferralRedemption> builder)
    {
        builder.ToTable("ReferralRedemptions");
        builder.Property(x => x.ReferredEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.ReferralCodeId, x.CreatedAt });
        builder.HasIndex(x => x.ReferredEmail);
    }
}

using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class IntegrationWebhookReceiptConfiguration : IEntityTypeConfiguration<IntegrationWebhookReceipt>
{
    public void Configure(EntityTypeBuilder<IntegrationWebhookReceipt> builder)
    {
        builder.ToTable("IntegrationWebhookReceipts");
        builder.Property(x => x.ProviderKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EventId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.State).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ErrorRedacted).HasMaxLength(500);
        builder.HasIndex(x => new { x.ProviderKey, x.EventId }).IsUnique();
    }
}

public class IntegrationDeadLetterConfiguration : IEntityTypeConfiguration<IntegrationDeadLetter>
{
    public void Configure(EntityTypeBuilder<IntegrationDeadLetter> builder)
    {
        builder.ToTable("IntegrationDeadLetters");
        builder.Property(x => x.ProviderKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Operation).HasMaxLength(80).IsRequired();
        builder.Property(x => x.EventId).HasMaxLength(200);
        builder.Property(x => x.ErrorRedacted).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }
}

public class IntegrationConversationConfiguration : IEntityTypeConfiguration<IntegrationConversation>
{
    public void Configure(EntityTypeBuilder<IntegrationConversation> builder)
    {
        builder.ToTable("IntegrationConversations");
        builder.Property(x => x.ProviderKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ExternalThreadId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerExternalId).HasMaxLength(200);
        builder.Property(x => x.CustomerDisplay).HasMaxLength(200);
        builder.Property(x => x.Channel).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.ProviderKey, x.ExternalThreadId }).IsUnique();
    }
}

public class IntegrationConversationMessageConfiguration : IEntityTypeConfiguration<IntegrationConversationMessage>
{
    public void Configure(EntityTypeBuilder<IntegrationConversationMessage> builder)
    {
        builder.ToTable("IntegrationConversationMessages");
        builder.Property(x => x.Direction).HasMaxLength(16).IsRequired();
        builder.Property(x => x.MessageType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ExternalMessageId).HasMaxLength(200);
        builder.Property(x => x.TemplateName).HasMaxLength(120);
        builder.Property(x => x.DeliveryStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.ConversationId, x.CreatedAt });
    }
}

public class CompanyPaymentRecordConfiguration : IEntityTypeConfiguration<CompanyPaymentRecord>
{
    public void Configure(EntityTypeBuilder<CompanyPaymentRecord> builder)
    {
        builder.ToTable("CompanyPaymentRecords");
        builder.Property(x => x.ProviderKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ExternalPaymentId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExternalCustomerId).HasMaxLength(200);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(12);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);
        builder.Property(x => x.LastEventId).HasMaxLength(200);
        builder.HasIndex(x => new { x.TenantId, x.ProviderKey, x.ExternalPaymentId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}

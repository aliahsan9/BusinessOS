using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable("ProcessedWebhookEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider).HasConversion<int>();
        builder.Property(x => x.EventId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.State).HasConversion<int>();

        builder.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
    }
}

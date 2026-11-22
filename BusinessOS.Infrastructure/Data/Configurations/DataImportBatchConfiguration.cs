using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public sealed class DataImportBatchConfiguration : IEntityTypeConfiguration<DataImportBatch>
{
    public void Configure(EntityTypeBuilder<DataImportBatch> builder)
    {
        builder.ToTable("DataImportBatches");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ImportedByUserId).HasMaxLength(450);
        builder.Property(x => x.ErrorSummaryJson).HasMaxLength(4000);
        builder.Property(x => x.ContentHash).HasMaxLength(64);
        builder.Property(x => x.FailedRowsJson); // nvarchar(max)

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.ContentHash });
    }
}
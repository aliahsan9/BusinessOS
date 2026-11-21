using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class ProcurementAgentSettingsConfiguration : IEntityTypeConfiguration<ProcurementAgentSettings>
{
    public void Configure(EntityTypeBuilder<ProcurementAgentSettings> builder)
    {
        builder.ToTable("ProcurementAgentSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PreferredSupplierCriterion).HasMaxLength(40).IsRequired();
        builder.Property(x => x.OverstockMultiplier).AsMoney();
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

public class ProcurementRecommendationConfiguration : IEntityTypeConfiguration<ProcurementRecommendation>
{
    public void Configure(EntityTypeBuilder<ProcurementRecommendation> builder)
    {
        builder.ToTable("ProcurementRecommendations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).AsMoney();
        builder.Property(x => x.UnitPrice).AsMoney();
        builder.Property(x => x.TotalAmount).AsMoney();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Confidence).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ApprovalFingerprint).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120);
        builder.Property(x => x.Source).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PaymentTerms).HasMaxLength(200);
        builder.Property(x => x.AssumptionsJson).HasMaxLength(4000);

        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ProductId });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Purchase).WithMany().HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PurchaseReceivingEventConfiguration : IEntityTypeConfiguration<PurchaseReceivingEvent>
{
    public void Configure(EntityTypeBuilder<PurchaseReceivingEvent> builder)
    {
        builder.ToTable("PurchaseReceivingEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReceivedByUserId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.PayloadJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.StockIncreasedBy).AsMoney();
        builder.Property(x => x.ResultStatus).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.PurchaseId });
        builder.HasOne(x => x.Purchase).WithMany().HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).AsMoney();
        builder.Property(x => x.PaymentMethod).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReferenceNo).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired().HasDefaultValue(PaymentStatusNames.Completed);
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired().HasDefaultValue("USD");
        builder.Property(x => x.RefundedAmount).AsMoney().HasDefaultValue(0m);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.ProviderOperationKey).HasMaxLength(128);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.PaymentDate })
            .IsDescending(false, true);
        builder.HasIndex(x => new { x.TenantId, x.CustomerId, x.PaymentDate })
            .IsDescending(false, false, true);
        builder.HasIndex(x => new { x.TenantId, x.OrderId });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.PaymentDate })
            .IsDescending(false, false, true);
    }
}

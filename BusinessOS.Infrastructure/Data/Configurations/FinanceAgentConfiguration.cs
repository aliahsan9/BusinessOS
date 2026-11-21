using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class FinanceAgentSettingsConfiguration : IEntityTypeConfiguration<FinanceAgentSettings>
{
    public void Configure(EntityTypeBuilder<FinanceAgentSettings> builder)
    {
        builder.ToTable("FinanceAgentSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReminderScheduleDays).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReminderChannel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReportingBasis).HasMaxLength(40).IsRequired();
        builder.Property(x => x.AnomalyVariancePercent).AsMoney();
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

public class FinanceBudgetConfiguration : IEntityTypeConfiguration<FinanceBudget>
{
    public void Configure(EntityTypeBuilder<FinanceBudget> builder)
    {
        builder.ToTable("FinanceBudgets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CategoryName).HasMaxLength(200);
        builder.Property(x => x.Amount).AsMoney();
        builder.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.PeriodStart, x.PeriodEnd });
        builder.HasOne(x => x.ExpenseCategory).WithMany().HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class FinancePaymentReminderConfiguration : IEntityTypeConfiguration<FinancePaymentReminder>
{
    public void Configure(EntityTypeBuilder<FinancePaymentReminder> builder)
    {
        builder.ToTable("FinancePaymentReminders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Channel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.OutstandingAmount).AsMoney();
        builder.Property(x => x.ApprovalFingerprint).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160);
        builder.Property(x => x.ExternalMessageId).HasMaxLength(200);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.InvoiceId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FinanceBankImportBatchConfiguration : IEntityTypeConfiguration<FinanceBankImportBatch>
{
    public void Configure(EntityTypeBuilder<FinanceBankImportBatch> builder)
    {
        builder.ToTable("FinanceBankImportBatches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Classification).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}

public class FinanceBankStatementLineConfiguration : IEntityTypeConfiguration<FinanceBankStatementLine>
{
    public void Configure(EntityTypeBuilder<FinanceBankStatementLine> builder)
    {
        builder.ToTable("FinanceBankStatementLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Amount).AsMoney();
        builder.Property(x => x.Direction).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(120);
        builder.Property(x => x.Fingerprint).HasMaxLength(128).IsRequired();
        builder.Property(x => x.MatchStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.Fingerprint }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.MatchStatus });
    }
}

public class FinanceAgentActivityConfiguration : IEntityTypeConfiguration<FinanceAgentActivity>
{
    public void Configure(EntityTypeBuilder<FinanceAgentActivity> builder)
    {
        builder.ToTable("FinanceAgentActivities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.PayloadJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.Severity });
    }
}

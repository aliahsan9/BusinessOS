using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusinessOS.Infrastructure.Data.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class CompanyTeamConfiguration : IEntityTypeConfiguration<CompanyTeam>
{
    public void Configure(EntityTypeBuilder<CompanyTeam> builder)
    {
        builder.ToTable("CompanyTeams");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.DepartmentId, x.Name });
    }
}

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.WorkedHours).HasPrecision(18, 2);
        builder.Property(x => x.CorrectionReason).HasMaxLength(500);
        builder.Property(x => x.CorrectedByUserId).HasMaxLength(450);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.WorkDate }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("LeaveTypes");
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.DefaultDays).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("LeaveBalances");
        builder.Property(x => x.Allocated).HasPrecision(18, 2);
        builder.Property(x => x.Used).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");
        builder.Property(x => x.Days).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.DecisionReason).HasMaxLength(1000);
        builder.Property(x => x.DecidedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Status });
    }
}

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Audience).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
    }
}

public class AnnouncementReadConfiguration : IEntityTypeConfiguration<AnnouncementRead>
{
    public void Configure(EntityTypeBuilder<AnnouncementRead> builder)
    {
        builder.ToTable("AnnouncementReads");
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.AnnouncementId, x.UserId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PerformanceGoalConfiguration : IEntityTypeConfiguration<PerformanceGoal>
{
    public void Configure(EntityTypeBuilder<PerformanceGoal> builder)
    {
        builder.ToTable("PerformanceGoals");
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.KpiName).HasMaxLength(120);
        builder.Property(x => x.TargetValue).HasPrecision(18, 2);
        builder.Property(x => x.CurrentValue).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.EmployeeId });
    }
}

public class PerformanceReviewConfiguration : IEntityTypeConfiguration<PerformanceReview>
{
    public void Configure(EntityTypeBuilder<PerformanceReview> builder)
    {
        builder.ToTable("PerformanceReviews");
        builder.Property(x => x.PeriodLabel).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ManagerUserId).HasMaxLength(450);
        builder.Property(x => x.Feedback).HasMaxLength(4000);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
    }
}

public class PipelineStageConfiguration : IEntityTypeConfiguration<PipelineStage>
{
    public void Configure(EntityTypeBuilder<PipelineStage> builder)
    {
        builder.ToTable("PipelineStages");
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.SortOrder });
    }
}

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.Source).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(40).IsRequired();
        builder.Property(x => x.AssignedUserId).HasMaxLength(450);
        builder.Property(x => x.EstimatedValue).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.Email });
    }
}

public class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.ToTable("Deals");
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AssignedUserId).HasMaxLength(450);
        builder.Property(x => x.Value).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TenantId, x.Status });
    }
}

public class StageTransitionConfiguration : IEntityTypeConfiguration<StageTransition>
{
    public void Configure(EntityTypeBuilder<StageTransition> builder)
    {
        builder.ToTable("StageTransitions");
        builder.Property(x => x.EntityType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
    }
}

public class FollowUpConfiguration : IEntityTypeConfiguration<FollowUp>
{
    public void Configure(EntityTypeBuilder<FollowUp> builder)
    {
        builder.ToTable("FollowUps");
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.AssignedUserId).HasMaxLength(450);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.DueAt, x.Status });
    }
}

public class CustomerSegmentConfiguration : IEntityTypeConfiguration<CustomerSegment>
{
    public void Configure(EntityTypeBuilder<CustomerSegment> builder)
    {
        builder.ToTable("CustomerSegments");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CustomerType).HasMaxLength(40);
        builder.Property(x => x.Industry).HasMaxLength(120);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.Tag).HasMaxLength(80);
        builder.Property(x => x.MinSpend).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class CustomerInteractionConfiguration : IEntityTypeConfiguration<CustomerInteraction>
{
    public void Configure(EntityTypeBuilder<CustomerInteraction> builder)
    {
        builder.ToTable("CustomerInteractions");
        builder.Property(x => x.Channel).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.CustomerId, x.OccurredAt });
    }
}

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(40);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[Code] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class WarehouseStockConfiguration : IEntityTypeConfiguration<WarehouseStock>
{
    public void Configure(EntityTypeBuilder<WarehouseStock> builder)
    {
        builder.ToTable("WarehouseStocks");
        builder.Property(x => x.OnHand).HasPrecision(18, 2);
        builder.Property(x => x.Reserved).HasPrecision(18, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.WarehouseId, x.ProductId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("StockTransfers");
        builder.Property(x => x.TransferNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.TransferNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> builder)
    {
        builder.ToTable("StockTransferLines");
        builder.Property(x => x.Quantity).HasPrecision(18, 2);
        builder.HasIndex(x => x.StockTransferId);
    }
}

public class OrderFulfillmentConfiguration : IEntityTypeConfiguration<OrderFulfillment>
{
    public void Configure(EntityTypeBuilder<OrderFulfillment> builder)
    {
        builder.ToTable("OrderFulfillments");
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Carrier).HasMaxLength(120);
        builder.Property(x => x.TrackingNumber).HasMaxLength(120);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.OrderId });
    }
}

public class FulfillmentLineConfiguration : IEntityTypeConfiguration<FulfillmentLine>
{
    public void Configure(EntityTypeBuilder<FulfillmentLine> builder)
    {
        builder.ToTable("FulfillmentLines");
        builder.Property(x => x.Quantity).HasPrecision(18, 2);
    }
}

public class SalesReturnConfiguration : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> builder)
    {
        builder.ToTable("SalesReturns");
        builder.Property(x => x.ReturnNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2);
        builder.Property(x => x.DecisionNotes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.ReturnNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SalesReturnLineConfiguration : IEntityTypeConfiguration<SalesReturnLine>
{
    public void Configure(EntityTypeBuilder<SalesReturnLine> builder)
    {
        builder.ToTable("SalesReturnLines");
        builder.Property(x => x.Quantity).HasPrecision(18, 2);
    }
}

public class IncomeRecordConfiguration : IEntityTypeConfiguration<IncomeRecord>
{
    public void Configure(EntityTypeBuilder<IncomeRecord> builder)
    {
        builder.ToTable("IncomeRecords");
        builder.Property(x => x.Category).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.PaymentMethod).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(120);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.OccurredOn });
    }
}

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("PurchaseInvoices");
        builder.Property(x => x.InvoiceNumber).HasMaxLength(60).IsRequired();
        builder.Property(x => x.SubTotal).HasPrecision(18, 2);
        builder.Property(x => x.Tax).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.AttachmentUrl).HasMaxLength(500);
        builder.Property(x => x.ExternalImportKey).HasMaxLength(100);
        builder.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.ExternalImportKey })
            .IsUnique()
            .HasFilter("[ExternalImportKey] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.ToTable("TaxRates");
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.RatePercent).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class OperationalSubscriptionConfiguration : IEntityTypeConfiguration<OperationalSubscription>
{
    public void Configure(EntityTypeBuilder<OperationalSubscription> builder)
    {
        builder.ToTable("OperationalSubscriptions");
        builder.Property(x => x.Provider).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.BillingCycle).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Cost).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.PaymentStatus).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ExpenseCategory).HasMaxLength(120);
    }
}

public class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.ToTable("PriceHistories");
        builder.Property(x => x.CostPrice).HasPrecision(18, 2);
        builder.Property(x => x.SalePrice).HasPrecision(18, 2);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450);
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.CreatedAt });
    }
}

public class ProjectMilestoneConfiguration : IEntityTypeConfiguration<ProjectMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectMilestone> builder)
    {
        builder.ToTable("ProjectMilestones");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.ProjectId });
    }
}

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.RoleName).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.UserId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("TimeEntries");
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Hours).HasPrecision(18, 2);
        builder.Property(x => x.HourlyRate).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
    }
}

public class WorkTaskCommentConfiguration : IEntityTypeConfiguration<WorkTaskComment>
{
    public void Configure(EntityTypeBuilder<WorkTaskComment> builder)
    {
        builder.ToTable("WorkTaskComments");
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
    }
}

public class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("Sprints");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.ProjectId });
    }
}

public class ProjectFileConfiguration : IEntityTypeConfiguration<ProjectFile>
{
    public void Configure(EntityTypeBuilder<ProjectFile> builder)
    {
        builder.ToTable("ProjectFiles");
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.UploadedByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.ProjectId });
    }
}

public class ClientPortalAccessConfiguration : IEntityTypeConfiguration<ClientPortalAccess>
{
    public void Configure(EntityTypeBuilder<ClientPortalAccess> builder)
    {
        builder.ToTable("ClientPortalAccesses");
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
    }
}

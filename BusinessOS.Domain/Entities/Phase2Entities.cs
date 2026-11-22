using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Department : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CompanyTeam : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? LeaderEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AttendanceRecord : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public string Status { get; set; } = "Present";
    public decimal WorkedHours { get; set; }
    public bool IsLate { get; set; }
    public bool IsEarlyDeparture { get; set; }
    public string? CorrectionReason { get; set; }
    public string? CorrectedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class LeaveType : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public decimal DefaultDays { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class LeaveBalance : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal Allocated { get; set; }
    public decimal Used { get; set; }
}

public class LeaveRequest : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Days { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Reason { get; set; }
    public string? DecisionReason { get; set; }
    public string? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class Announcement : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string Audience { get; set; } = "Company";
    public Guid? DepartmentId { get; set; }
    public Guid? CompanyTeamId { get; set; }
    public string Priority { get; set; } = "Normal";
    public DateTime? ExpiresAt { get; set; }
    public bool IsPublished { get; set; } = true;
    public string CreatedByUserId { get; set; } = default!;
}

public class AnnouncementRead : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid AnnouncementId { get; set; }
    public string UserId { get; set; } = default!;
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}

public class PerformanceGoal : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? KpiName { get; set; }
    public decimal TargetValue { get; set; }
    public decimal CurrentValue { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public string Status { get; set; } = "Active";
}

public class PerformanceReview : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string PeriodLabel { get; set; } = default!;
    public string? ManagerUserId { get; set; }
    public int Rating { get; set; }
    public string? Feedback { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? CompletedAt { get; set; }
}

public class PipelineStage : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsWon { get; set; }
    public bool IsLost { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Lead : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Source { get; set; } = "Manual";
    public string Status { get; set; } = "New";
    public string Priority { get; set; } = "Medium";
    public Guid? StageId { get; set; }
    public string? AssignedUserId { get; set; }
    public decimal EstimatedValue { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public string? Notes { get; set; }
    public bool IsQualified { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? DealId { get; set; }
}

public class Deal : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = default!;
    public Guid? LeadId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? StageId { get; set; }
    public string? AssignedUserId { get; set; }
    public decimal Value { get; set; }
    public int Probability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public string Status { get; set; } = "Open";
    public string? Notes { get; set; }
    public Guid? ProjectId { get; set; }
}

public class StageTransition : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public Guid? FromStageId { get; set; }
    public Guid? ToStageId { get; set; }
    public string? UserId { get; set; }
}

public class FollowUp : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = default!;
    public DateTime DueAt { get; set; }
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Open";
    public string? AssignedUserId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? DealId { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CustomerSegment : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string? CustomerType { get; set; }
    public string? Industry { get; set; }
    public string? Country { get; set; }
    public string? Tag { get; set; }
    public decimal? MinSpend { get; set; }
    public string? Notes { get; set; }
}

public class CustomerInteraction : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? DealId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Channel { get; set; } = "Note";
    public string Summary { get; set; } = default!;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
}

public class Warehouse : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = default!;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class WarehouseStock : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class StockTransfer : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string TransferNumber { get; set; } = default!;
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Notes { get; set; }
}

public class StockTransferLine : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid StockTransferId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class OrderFulfillment : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? Notes { get; set; }
}

public class FulfillmentLine : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid OrderFulfillmentId { get; set; }
    public Guid OrderItemId { get; set; }
    public decimal Quantity { get; set; }
}

public class SalesReturn : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string ReturnNumber { get; set; } = default!;
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = "Requested";
    public string Reason { get; set; } = default!;
    public bool Restock { get; set; }
    public decimal RefundAmount { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? DecisionNotes { get; set; }
}

public class SalesReturnLine : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid SalesReturnId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class IncomeRecord : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Category { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime OccurredOn { get; set; }
    public string PaymentMethod { get; set; } = "Other";
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Approved";
    public Guid? InvoiceId { get; set; }
    public bool IsRecurring { get; set; }
}

public class PurchaseInvoice : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public Guid SupplierId { get; set; }
    public Guid? PurchaseId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = "Issued";
    public string? Notes { get; set; }
    public string? AttachmentUrl { get; set; }

    /// <summary>Stable key from historical supplier-payment import.</summary>
    public string? ExternalImportKey { get; set; }
}

public class TaxRate : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public decimal RatePercent { get; set; }
    public bool IsInclusive { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class OperationalSubscription : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Provider { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string BillingCycle { get; set; } = "Monthly";
    public decimal Cost { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime RenewalDate { get; set; }
    public string PaymentStatus { get; set; } = "Pending";
    public string? ExpenseCategory { get; set; }
}

public class PriceHistory : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string? ChangedByUserId { get; set; }
}

public class ProjectMilestone : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = default!;
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Open";
    public bool IsClientVisible { get; set; } = true;
}

public class ProjectMember : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = default!;
    public string RoleName { get; set; } = "Contributor";
}

public class TimeEntry : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? WorkTaskId { get; set; }
    public string UserId { get; set; } = default!;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public decimal Hours { get; set; }
    public bool IsBillable { get; set; }
    public decimal? HourlyRate { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class WorkTaskComment : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkTaskId { get; set; }
    public string UserId { get; set; } = default!;
    public string Body { get; set; } = default!;
    public bool IsClientVisible { get; set; }
}

public class Sprint : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = default!;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Planned";
}

public class ProjectFile : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = default!;
    public string UploadedByUserId { get; set; } = default!;
    public bool IsClientVisible { get; set; }
    public int Version { get; set; } = 1;
}

public class ClientPortalAccess : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Email { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

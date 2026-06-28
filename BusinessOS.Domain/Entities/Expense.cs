using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Expense : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string Title { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }

    public Guid ExpenseCategoryId { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? Vendor { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public string? ReceiptUrl { get; set; }
    public string Status { get; set; } = "Pending";
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }

    public ExpenseCategory ExpenseCategory { get; set; } = default!;
}

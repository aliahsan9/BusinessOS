using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Expense : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string Title { get; set; } = default!;
    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; }

    public string Category { get; set; } = default!;
}

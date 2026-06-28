using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class ExpenseCategory : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

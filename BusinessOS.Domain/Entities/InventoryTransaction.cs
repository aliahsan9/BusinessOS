using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class InventoryTransaction : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public string TransactionType { get; set; } = default!;
    // Purchase / Sale / Return / Adjustment

    public Guid? ReferenceId { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = default!;
}

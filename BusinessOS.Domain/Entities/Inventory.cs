using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Inventory : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public decimal CurrentStock { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public decimal MaximumStockLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = default!;
}

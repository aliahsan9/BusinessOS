using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Product : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CategoryId { get; set; }

    public string Name { get; set; } = default!;
    public string SKU { get; set; } = default!;

    public string? Description { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }

    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }

    public bool IsActive { get; set; } = true;

    public Category Category { get; set; } = default!;
}

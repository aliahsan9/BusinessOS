using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class PurchaseItem : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }

    public Purchase Purchase { get; set; } = default!;
    public Product? Product { get; set; }
}

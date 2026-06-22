using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class OrderItem : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }

    public Order Order { get; set; } = default!;
    public Product? Product { get; set; }
}

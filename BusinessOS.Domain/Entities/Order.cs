using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Order : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }

    public string OrderNumber { get; set; } = default!;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Pending";

    public decimal TotalAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }

    public Customer Customer { get; set; } = default!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

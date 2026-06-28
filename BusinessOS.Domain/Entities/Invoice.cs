using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Invoice : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string InvoiceNumber { get; set; } = default!;
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = "Draft";

    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal OutstandingAmount { get; set; }

    public string? Notes { get; set; }

    public Order Order { get; set; } = default!;
    public Customer Customer { get; set; } = default!;
}

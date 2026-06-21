using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Payment : AuditableEntity
{
    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }

    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    public string? ReferenceNo { get; set; }

    public Order Order { get; set; } = default!;
    public Customer Customer { get; set; } = default!;
}

using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

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

    public string Status { get; set; } = PaymentStatusNames.Completed;

    public string Currency { get; set; } = "USD";

    public decimal RefundedAmount { get; set; }

    public string? ProviderReference { get; set; }

    public string? ProviderOperationKey { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public Order Order { get; set; } = default!;
    public Customer Customer { get; set; } = default!;
}

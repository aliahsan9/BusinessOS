using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class BillingTransaction : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid? BillingInvoiceId { get; set; }
    public string TransactionId { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public BillingTransactionStatus Status { get; set; } = BillingTransactionStatus.Pending;
    public PaymentProviderType Provider { get; set; } = PaymentProviderType.Stripe;
    public string? ProviderReference { get; set; }
    public string? Description { get; set; }
    public string? Metadata { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = default!;
    public BillingInvoice? Invoice { get; set; }
}

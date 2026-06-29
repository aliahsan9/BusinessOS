using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class BillingInvoice : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public Guid SubscriptionPlanId { get; set; }
    public string PlanName { get; set; } = default!;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public BillingInvoiceStatus Status { get; set; } = BillingInvoiceStatus.Draft;
    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
    public PaymentProviderType PaymentProvider { get; set; } = PaymentProviderType.Stripe;
    public string? PaymentMethod { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ExternalInvoiceId { get; set; }
    public string? Notes { get; set; }

    public Tenant Tenant { get; set; } = default!;
}

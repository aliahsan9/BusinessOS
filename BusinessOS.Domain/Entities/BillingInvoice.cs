using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Platform billing invoice issued to a tenant for subscription charges.
/// </summary>
/// <remarks>
/// Tenant-scoped. Represents BusinessOS platform billing (not tenant customer invoices).
/// Links to <see cref="Tenant"/> and records subscription plan, billing period, and payment provider details.
/// </remarks>
public class BillingInvoice : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant being billed. Required. Foreign key to <see cref="Tenant"/>.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Unique invoice number displayed to the tenant. Required.
    /// </summary>
    public string InvoiceNumber { get; set; } = default!;

    /// <summary>
    /// Foreign key to the <see cref="SubscriptionPlan"/> billed on this invoice. Required.
    /// </summary>
    public Guid SubscriptionPlanId { get; set; }

    /// <summary>
    /// Denormalized plan name at the time of invoicing for historical reference. Required.
    /// </summary>
    public string PlanName { get; set; } = default!;

    /// <summary>
    /// Subtotal amount before tax. Must be non-negative.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Tax amount applied to the subtotal. Must be non-negative.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Total amount due including tax. Must equal subtotal plus tax.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// ISO currency code for all monetary amounts. Defaults to USD.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Current billing invoice status. Defaults to Draft.
    /// </summary>
    public BillingInvoiceStatus Status { get; set; } = BillingInvoiceStatus.Draft;

    /// <summary>
    /// Billing frequency for this invoice (monthly or annual). Defaults to Monthly.
    /// </summary>
    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;

    /// <summary>
    /// Payment provider used to collect payment. Defaults to Stripe.
    /// </summary>
    public PaymentProviderType PaymentProvider { get; set; } = PaymentProviderType.Stripe;

    /// <summary>
    /// Optional description of the payment method used (for example, card ending 4242).
    /// </summary>
    public string? PaymentMethod { get; set; }

    /// <summary>
    /// Start of the billing period covered by this invoice. Required.
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End of the billing period covered by this invoice. Required.
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// UTC timestamp when payment was received, or null if unpaid.
    /// </summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// Optional external invoice identifier from the payment provider (for example, Stripe invoice id).
    /// </summary>
    public string? ExternalInvoiceId { get; set; }

    /// <summary>
    /// Optional free-text notes attached to the invoice.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Navigation property to the tenant being billed.
    /// </summary>
    public Tenant Tenant { get; set; } = default!;
}

using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Payment transaction record for platform billing charges against a tenant.
/// </summary>
/// <remarks>
/// Tenant-scoped. May optionally link to a <see cref="BillingInvoice"/> when the transaction settles an invoice.
/// Tracks provider references and completion status for reconciliation.
/// </remarks>
public class BillingTransaction : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant associated with this transaction. Required. Foreign key to <see cref="Tenant"/>.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Optional foreign key to the related <see cref="BillingInvoice"/> being paid.
    /// </summary>
    public Guid? BillingInvoiceId { get; set; }

    /// <summary>
    /// Unique transaction identifier (internal or provider-assigned). Required.
    /// </summary>
    public string TransactionId { get; set; } = default!;

    /// <summary>
    /// Transaction amount in the specified currency. Must be non-negative.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// ISO currency code for the transaction amount. Defaults to USD.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Current transaction status. Defaults to Pending.
    /// </summary>
    public BillingTransactionStatus Status { get; set; } = BillingTransactionStatus.Pending;

    /// <summary>
    /// Payment provider that processed this transaction. Defaults to Stripe.
    /// </summary>
    public PaymentProviderType Provider { get; set; } = PaymentProviderType.Stripe;

    /// <summary>
    /// Optional provider-specific reference identifier for reconciliation (for example, payment intent id).
    /// </summary>
    public string? ProviderReference { get; set; }

    /// <summary>
    /// Optional human-readable description of the transaction.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional JSON metadata with additional provider or business context.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// UTC timestamp when the transaction completed successfully, or null if not yet completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Navigation property to the tenant associated with this transaction.
    /// </summary>
    public Tenant Tenant { get; set; } = default!;

    /// <summary>
    /// Navigation property to the related billing invoice, if any.
    /// </summary>
    public BillingInvoice? Invoice { get; set; }
}

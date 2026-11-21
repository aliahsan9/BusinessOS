using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Purchase : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid SupplierId { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = default!;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    /// <summary>Expected arrival date used for delivery monitoring.</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>Prevents duplicate agent-created purchase orders for the same intent.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Hash of supplier + lines + prices approved for this purchase.</summary>
    public string? ApprovalFingerprint { get; set; }

    public string? PaymentTerms { get; set; }

    /// <summary>True only when an approved external supplier transmission succeeded.</summary>
    public bool SentToSupplier { get; set; }

    public DateTime? DelayedNotifiedAt { get; set; }

    public Supplier Supplier { get; set; } = default!;
    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}

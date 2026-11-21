using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class PurchaseItem : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }

    /// <summary>Verified received quantity. Stock increases only from verified receiving events.</summary>
    public decimal ReceivedQuantity { get; set; }

    public decimal DamagedQuantity { get; set; }
    public decimal ShortageQuantity { get; set; }
    public string? ReceivingNotes { get; set; }

    public bool IsDeleted { get; set; } = false;
    public Purchase Purchase { get; set; } = default!;
    public Product? Product { get; set; }
}

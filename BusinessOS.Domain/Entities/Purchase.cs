using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Purchase : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid SupplierId { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

    public decimal TotalAmount { get; set; }

    public Supplier Supplier { get; set; } = default!;
    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}

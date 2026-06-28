using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class QuotationItem : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid QuotationId { get; set; }
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public bool IsDeleted { get; set; } = false;

    public Quotation Quotation { get; set; } = default!;
    public Product? Product { get; set; }
}

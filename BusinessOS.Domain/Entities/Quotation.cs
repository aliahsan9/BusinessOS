using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Quotation : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string QuotationNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }

    public DateTime QuotationDate { get; set; } = DateTime.UtcNow;
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = "Draft";

    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }

    public string? Notes { get; set; }

    public Customer Customer { get; set; } = default!;
    public ICollection<QuotationItem> QuotationItems { get; set; } = new List<QuotationItem>();
}

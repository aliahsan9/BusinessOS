using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class StockTransaction : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public string TransactionType { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal PreviousStock { get; set; }
    public decimal NewStock { get; set; }

    public string? ReferenceNumber { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public string? UserId { get; set; }

    public Product? Product { get; set; }
}

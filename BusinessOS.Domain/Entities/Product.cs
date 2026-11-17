using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class Product : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid CategoryId { get; set; }

    public string Name { get; set; } = default!;
    public string SKU { get; set; } = default!;

    public string? Description { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }

    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }

    public bool IsActive { get; set; } = true;
    public string Kind { get; set; } = "Physical";
    public string? Barcode { get; set; }
    public string? UnitOfMeasure { get; set; }
    public string? ImageUrl { get; set; }
    public string? PricingModel { get; set; }
    public string? Duration { get; set; }
    public string? DeliveryDetails { get; set; }
    public Guid? DefaultTaxRateId { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public Category Category { get; set; } = default!;
    public Inventory? Inventory { get; set; }
}

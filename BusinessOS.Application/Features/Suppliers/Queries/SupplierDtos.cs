namespace BusinessOS.Application.Features.Suppliers.Queries;

public class SupplierResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string? ContactPerson { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SupplierSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string? ContactPerson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupplierPurchaseSummaryResponse
{
    public Guid Id { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = default!;
    public int ItemCount { get; set; }
}

public class SupplierProductSummaryResponse
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string ProductSku { get; set; } = default!;
    public DateTime? LastPurchaseDate { get; set; }
    public decimal TotalQuantityPurchased { get; set; }
}

public record CreateSupplierRequest(
    string Name,
    string Email,
    string Phone,
    string Address,
    string? ContactPerson,
    string? Notes);

public record UpdateSupplierRequest(
    string Name,
    string Email,
    string Phone,
    string Address,
    string? ContactPerson,
    string? Notes);

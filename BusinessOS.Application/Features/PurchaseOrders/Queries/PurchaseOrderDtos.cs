namespace BusinessOS.Application.Features.PurchaseOrders.Queries;

public class PurchaseOrderSummaryResponse
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = default!;
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = default!;
    public string? ReferenceNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PurchaseOrderResponse
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = default!;
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = default!;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<PurchaseOrderLineItemResponse> Items { get; set; } = Array.Empty<PurchaseOrderLineItemResponse>();
}

public class PurchaseOrderLineItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string ProductSku { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public record CreatePurchaseOrderLineItemDto(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice);

public record CreatePurchaseOrderRequest(
    Guid SupplierId,
    DateTime PurchaseDate,
    string Status,
    string? ReferenceNumber,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderLineItemDto> Items);

public record UpdatePurchaseOrderRequest(
    Guid SupplierId,
    DateTime PurchaseDate,
    string Status,
    string? ReferenceNumber,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderLineItemDto> Items);

public record UpdatePurchaseOrderStatusRequest(string Status);

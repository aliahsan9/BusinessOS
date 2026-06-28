namespace BusinessOS.Application.Features.Quotations.Queries;

public class QuotationSummaryResponse
{
    public Guid Id { get; set; }
    public string QuotationNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public DateTime QuotationDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = default!;
    public decimal GrandTotal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuotationResponse
{
    public Guid Id { get; set; }
    public string QuotationNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public DateTime QuotationDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = default!;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<QuotationLineItemResponse> Items { get; set; } = Array.Empty<QuotationLineItemResponse>();
}

public class QuotationLineItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string ProductSku { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public record QuotationLineItemDto(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice);

public record CreateQuotationRequest(
    Guid CustomerId,
    DateTime QuotationDate,
    DateTime ExpiryDate,
    string Status,
    decimal Discount,
    decimal Tax,
    string? Notes,
    IReadOnlyList<QuotationLineItemDto> Items);

public record UpdateQuotationRequest(
    Guid CustomerId,
    DateTime QuotationDate,
    DateTime ExpiryDate,
    string Status,
    decimal Discount,
    decimal Tax,
    string? Notes,
    IReadOnlyList<QuotationLineItemDto> Items);

public record UpdateQuotationStatusRequest(string Status);

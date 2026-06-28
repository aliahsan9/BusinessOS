namespace BusinessOS.Application.Features.Orders.Queries;

public class OrderSummaryDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = default!;
    public DateTime OrderDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = default!;
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public decimal GrandTotal { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string OrderNumber { get; set; } = default!;
    public DateTime OrderDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Status { get; set; } = default!;
    public string CustomerName { get; set; } = default!;
    public string CustomerFirstName { get; set; } = default!;
    public string CustomerLastName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public string CustomerPhone { get; set; } = default!;
    public string CustomerAddress { get; set; } = default!;
    public string CustomerCity { get; set; } = default!;
    public string CustomerCountry { get; set; } = default!;
    public decimal TotalAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
    public IReadOnlyList<OrderItemDto> Items { get; set; } = Array.Empty<OrderItemDto>();
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string ProductSku { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public record CreateOrderItemDto(Guid ProductId, decimal Quantity);

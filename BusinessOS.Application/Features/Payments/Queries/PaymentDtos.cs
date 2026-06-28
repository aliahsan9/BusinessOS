namespace BusinessOS.Application.Features.Payments.Queries;

public class PaymentSummaryResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public DateTime PaymentDate { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public DateTime PaymentDate { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public record CreatePaymentRequest(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string PaymentMethod,
    DateTime PaymentDate,
    string? ReferenceNo);

public record UpdatePaymentRequest(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string PaymentMethod,
    DateTime PaymentDate,
    string? ReferenceNo);

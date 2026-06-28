namespace BusinessOS.Application.Features.Customers.Queries;

public class CustomerResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string PostalCode { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CustomerSummaryResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerOrderResponse
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = default!;
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = default!;
    public decimal GrandTotal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerAnalyticsResponse
{
    public int TotalOrders { get; set; }
    public decimal TotalSpending { get; set; }
    public decimal AverageOrderValue { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public int TotalCompletedOrders { get; set; }
}

public record CreateCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Address,
    string City,
    string Country,
    string PostalCode);

public record UpdateCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Address,
    string City,
    string Country,
    string PostalCode,
    bool IsActive);

namespace BusinessOS.Application.Features.Expenses.Queries;

public class ExpenseResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public string PaymentMethod { get; set; } = default!;
    public string? Vendor { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public string? ReceiptUrl { get; set; }
    public string Status { get; set; } = default!;
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ExpenseSummaryResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string CategoryName { get; set; } = default!;
    public string PaymentMethod { get; set; } = default!;
    public string? Vendor { get; set; }
    public string Status { get; set; } = default!;
    public bool IsRecurring { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExpenseAnalyticsResponse
{
    public decimal TotalExpenses { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<ExpenseCategoryBreakdown> TopCategories { get; set; } = [];
    public IReadOnlyList<ExpenseTrendPoint> Trends { get; set; } = [];
}

public class ExpenseCategoryBreakdown
{
    public string CategoryName { get; set; } = default!;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class ExpenseTrendPoint
{
    public string Period { get; set; } = default!;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public record CreateExpenseRequest(
    string Title,
    decimal Amount,
    DateTime ExpenseDate,
    Guid ExpenseCategoryId,
    string PaymentMethod,
    string? Vendor,
    string? ReferenceNumber,
    string? Description,
    string? ReceiptUrl,
    string Status,
    bool IsRecurring,
    string? RecurrencePattern);

public record UpdateExpenseRequest(
    string Title,
    decimal Amount,
    DateTime ExpenseDate,
    Guid ExpenseCategoryId,
    string PaymentMethod,
    string? Vendor,
    string? ReferenceNumber,
    string? Description,
    string? ReceiptUrl,
    string Status,
    bool IsRecurring,
    string? RecurrencePattern);

public class ExpenseCategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ExpenseCount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record CreateExpenseCategoryRequest(string Name, string? Description);

public record UpdateExpenseCategoryRequest(string Name, string? Description, bool IsActive);

namespace BusinessOS.Application.Features.Finance.DTOs;

public class FinancialDashboardResponse
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal OutstandingInvoices { get; set; }
    public int CompletedOrders { get; set; }
    public int TotalExpensesCount { get; set; }
    public string Period { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public IReadOnlyList<TrendPoint> RevenueTrend { get; set; } = [];
    public IReadOnlyList<TrendPoint> ExpenseTrend { get; set; } = [];
}

public class ProfitLossResponse
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal NetProfit { get; set; }
    public string GroupBy { get; set; } = default!;
    public string Period { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public IReadOnlyList<TrendPoint> PeriodBreakdown { get; set; } = [];
}

public class RevenueBreakdown
{
    public decimal OrderRevenue { get; set; }
    public decimal PaymentTotal { get; set; }
    public IReadOnlyList<RevenueCategoryItem> ByPaymentMethod { get; set; } = [];
    public IReadOnlyList<TrendPoint> Trends { get; set; } = [];
    public string Period { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class RevenueCategoryItem
{
    public string Category { get; set; } = default!;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class ExpenseBreakdown
{
    public decimal TotalExpenses { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<ExpenseCategoryItem> ByCategory { get; set; } = [];
    public IReadOnlyList<TrendPoint> Trends { get; set; } = [];
    public string Period { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class ExpenseCategoryItem
{
    public string CategoryName { get; set; } = default!;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class TrendPoint
{
    public string Period { get; set; } = default!;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

namespace BusinessOS.Application.Features.Pdf.Models;

public sealed class BusinessHeaderInfo
{
    public string BusinessName { get; init; } = default!;
    public string? LogoUrl { get; init; }
    public string OwnerName { get; init; } = default!;
    public string? Address { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string Currency { get; init; } = "USD";
}

public sealed class PdfKeyValueRow
{
    public string Label { get; init; } = default!;
    public string Value { get; init; } = default!;
}

public sealed class PdfTableRow
{
    public IReadOnlyList<string> Cells { get; init; } = [];
}

public sealed class PdfChartSnapshot
{
    public string Title { get; init; } = default!;
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<decimal> Values { get; init; } = [];
}

public sealed class BusinessSummaryPdfModel
{
    public BusinessHeaderInfo Header { get; init; } = default!;
    public DateTime ReportDate { get; init; }
    public string PeriodLabel { get; init; } = default!;
    public IReadOnlyList<PdfKeyValueRow> Statistics { get; init; } = [];
    public IReadOnlyList<PdfChartSnapshot> Charts { get; init; } = [];
}

public sealed class RevenueReportPdfModel
{
    public BusinessHeaderInfo Header { get; init; } = default!;
    public DateTime ReportDate { get; init; }
    public string PeriodLabel { get; init; } = default!;
    public decimal TotalRevenue { get; init; }
    public IReadOnlyList<PdfTableRow> RevenueByMonth { get; init; } = [];
    public IReadOnlyList<PdfTableRow> RevenueByCustomer { get; init; } = [];
    public IReadOnlyList<PdfTableRow> RevenueByProject { get; init; } = [];
}

public sealed class ExpenseReportPdfModel
{
    public BusinessHeaderInfo Header { get; init; } = default!;
    public DateTime ReportDate { get; init; }
    public string PeriodLabel { get; init; } = default!;
    public decimal TotalExpenses { get; init; }
    public IReadOnlyList<PdfTableRow> ByCategory { get; init; } = [];
    public IReadOnlyList<PdfTableRow> MonthlyExpenses { get; init; } = [];
    public IReadOnlyList<PdfTableRow> Trends { get; init; } = [];
}

public sealed class ProfitLossPdfModel
{
    public BusinessHeaderInfo Header { get; init; } = default!;
    public DateTime ReportDate { get; init; }
    public string PeriodLabel { get; init; } = default!;
    public decimal TotalRevenue { get; init; }
    public decimal TotalExpenses { get; init; }
    public decimal NetProfit { get; init; }
    public decimal ProfitMarginPercent { get; init; }
    public IReadOnlyList<PdfTableRow> PeriodBreakdown { get; init; } = [];
}

public sealed class CustomerReportPdfModel
{
    public BusinessHeaderInfo Header { get; init; } = default!;
    public DateTime ReportDate { get; init; }
    public string PeriodLabel { get; init; } = default!;
    public IReadOnlyList<PdfTableRow> Customers { get; init; } = [];
}

public sealed class ProjectReportPdfModel
{
    public BusinessHeaderInfo Header { get; init; } = default!;
    public DateTime ReportDate { get; init; }
    public IReadOnlyList<PdfKeyValueRow> ProjectInfo { get; init; } = [];
    public IReadOnlyList<PdfTableRow> Tasks { get; init; } = [];
    public IReadOnlyList<string> AssignedMembers { get; init; } = [];
}

public sealed class TaskReportPdfModel
{
    public BusinessHeaderInfo Header { get; init; } = default!;
    public DateTime ReportDate { get; init; }
    public string PeriodLabel { get; init; } = default!;
    public int CompletedTasks { get; init; }
    public int PendingTasks { get; init; }
    public int OverdueTasks { get; init; }
    public decimal CompletionRate { get; init; }
}

public sealed class InvoiceLineItem
{
    public string Description { get; init; } = default!;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Total { get; init; }
}

public sealed class InvoicePdfModel
{
    public BusinessHeaderInfo Header { get; init; } = default!;
    public string InvoiceNumber { get; init; } = default!;
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public string Status { get; init; } = default!;
    public string CustomerName { get; init; } = default!;
    public string? CustomerEmail { get; init; }
    public string? CustomerAddress { get; init; }
    public IReadOnlyList<InvoiceLineItem> LineItems { get; init; } = [];
    public decimal SubTotal { get; init; }
    public decimal Discount { get; init; }
    public decimal Tax { get; init; }
    public decimal GrandTotal { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal OutstandingAmount { get; init; }
    public string? Notes { get; init; }
}

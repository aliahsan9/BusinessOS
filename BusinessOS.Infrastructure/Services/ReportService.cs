using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Analytics.Services;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Finance.Services;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Application.Features.Pdf.Models;
using BusinessOS.Application.Features.Pdf.Services;
using BusinessOS.Application.Features.Reports.DTOs;
using BusinessOS.Application.Features.Reports.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class ReportService : IReportService
{
    private static readonly string[] ExcludedInvoiceStatuses =
    [
        InvoiceStatusNames.Draft,
        InvoiceStatusNames.Cancelled
    ];

    private readonly IApplicationDbContext _context;
    private readonly IAnalyticsModuleService _analytics;
    private readonly IFinanceService _finance;
    private readonly IPdfGenerationService _pdf;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportService(
        IApplicationDbContext context,
        IAnalyticsModuleService analytics,
        IFinanceService finance,
        IPdfGenerationService pdf,
        ICurrentUserService currentUser,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _analytics = analytics;
        _finance = finance;
        _pdf = pdf;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public Task<ReportGenerationResult> GenerateBusinessSummaryAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(
            ReportTypes.BusinessSummary,
            "Business Summary Report",
            $"Business-Summary-{DateTime.UtcNow:yyyyMMdd}.pdf",
            query,
            null,
            async ct =>
            {
                var header = await GetHeaderAsync(ct);
                var overview = await _analytics.GetOverviewAsync(query.StartDate, query.EndDate, query.Period, ct);
                var revenueChart = await _analytics.GetRevenueChartAsync(query.StartDate, query.EndDate, query.Period, ct);
                var expenseChart = await _analytics.GetExpenseChartAsync(query.StartDate, query.EndDate, query.Period, ct);
                var profitChart = await _analytics.GetProfitChartAsync(query.StartDate, query.EndDate, query.Period, ct);

                var stats = new List<PdfKeyValueRow>
                {
                    new() { Label = "Total Customers", Value = overview.TotalCustomers.Value.ToString("N0") },
                    new() { Label = "Total Projects", Value = overview.ActiveProjects.Value.ToString("N0") },
                    new() { Label = "Total Tasks", Value = overview.TotalTasks.Value.ToString("N0") },
                    new() { Label = "Completed Tasks", Value = overview.CompletedTasks.Value.ToString("N0") },
                    new() { Label = "Total Revenue", Value = FormatMoney(overview.TotalRevenue.Value, header.Currency) },
                    new() { Label = "Total Expenses", Value = FormatMoney(overview.TotalExpenses.Value, header.Currency) },
                    new() { Label = "Net Profit", Value = FormatMoney(overview.NetProfit.Value, header.Currency) },
                    new() { Label = "Total Invoices", Value = overview.TotalInvoices.Value.ToString("N0") }
                };

                var charts = new List<PdfChartSnapshot>
                {
                    ToChartSnapshot(revenueChart),
                    ToChartSnapshot(expenseChart),
                    ToChartSnapshot(profitChart.Datasets.FirstOrDefault()?.Label ?? "Profit", profitChart)
                };

                var model = new BusinessSummaryPdfModel
                {
                    Header = header,
                    ReportDate = DateTime.UtcNow,
                    PeriodLabel = FormatPeriodLabel(overview.DateRange),
                    Statistics = stats,
                    Charts = charts
                };

                return _pdf.GenerateBusinessSummary(model);
            },
            cancellationToken);

    public Task<ReportGenerationResult> GenerateRevenueReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(
            ReportTypes.Revenue,
            "Revenue Report",
            $"Revenue-Report-{DateTime.UtcNow:yyyyMMdd}.pdf",
            query,
            null,
            async ct =>
            {
                var header = await GetHeaderAsync(ct);
                var range = await ResolveRangeAsync(query, ct);
                var revenueChart = await _analytics.GetRevenueChartAsync(query.StartDate, query.EndDate, query.Period, ct);
                var topCustomers = await _analytics.GetTopCustomersAsync(query.StartDate, query.EndDate, query.Period, 20, ct);

                var revenueByProject = await _context.Invoices
                    .AsNoTracking()
                    .Where(x => x.InvoiceDate >= range.StartDate
                        && x.InvoiceDate <= range.EndDate
                        && !ExcludedInvoiceStatuses.Contains(x.Status))
                    .GroupBy(x => new { x.OrderId, x.Order.OrderNumber })
                    .Select(g => new { g.Key.OrderNumber, Revenue = g.Sum(x => x.GrandTotal) })
                    .OrderByDescending(x => x.Revenue)
                    .Take(20)
                    .ToListAsync(ct);

                var totalRevenue = revenueChart.Datasets.FirstOrDefault()?.Data.Sum() ?? 0;

                var model = new RevenueReportPdfModel
                {
                    Header = header,
                    ReportDate = DateTime.UtcNow,
                    PeriodLabel = FormatPeriodLabel(revenueChart.DateRange),
                    TotalRevenue = totalRevenue,
                    RevenueByMonth = revenueChart.Labels
                        .Select((label, i) => new PdfTableRow
                        {
                            Cells =
                            [
                                label,
                                FormatMoney(revenueChart.Datasets.FirstOrDefault()?.Data.ElementAtOrDefault(i) ?? 0, header.Currency)
                            ]
                        })
                        .ToList(),
                    RevenueByCustomer = topCustomers.Customers
                        .Select(c => new PdfTableRow
                        {
                            Cells = [c.CustomerName, FormatMoney(c.RevenueGenerated, header.Currency)]
                        })
                        .ToList(),
                    RevenueByProject = revenueByProject
                        .Select(p => new PdfTableRow
                        {
                            Cells = [p.OrderNumber, FormatMoney(p.Revenue, header.Currency)]
                        })
                        .ToList()
                };

                return _pdf.GenerateRevenueReport(model);
            },
            cancellationToken);

    public Task<ReportGenerationResult> GenerateExpenseReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(
            ReportTypes.Expenses,
            "Expense Report",
            $"Expense-Report-{DateTime.UtcNow:yyyyMMdd}.pdf",
            query,
            null,
            async ct =>
            {
                var header = await GetHeaderAsync(ct);
                var breakdown = await _finance.GetExpenseBreakdownAsync(query.StartDate, query.EndDate, query.Period, ct);
                var expenseChart = await _analytics.GetExpenseChartAsync(query.StartDate, query.EndDate, query.Period, ct);

                var model = new ExpenseReportPdfModel
                {
                    Header = header,
                    ReportDate = DateTime.UtcNow,
                    PeriodLabel = FormatPeriodLabel(breakdown.StartDate, breakdown.EndDate, breakdown.Period),
                    TotalExpenses = breakdown.TotalExpenses,
                    ByCategory = breakdown.ByCategory
                        .Select(c => new PdfTableRow
                        {
                            Cells = [c.CategoryName, FormatMoney(c.Amount, header.Currency), c.Count.ToString()]
                        })
                        .ToList(),
                    MonthlyExpenses = expenseChart.Labels
                        .Select((label, i) => new PdfTableRow
                        {
                            Cells =
                            [
                                label,
                                FormatMoney(expenseChart.Datasets.FirstOrDefault()?.Data.ElementAtOrDefault(i) ?? 0, header.Currency)
                            ]
                        })
                        .ToList(),
                    Trends = breakdown.Trends
                        .Select(t => new PdfTableRow
                        {
                            Cells = [t.Period, FormatMoney(t.Amount, header.Currency)]
                        })
                        .ToList()
                };

                return _pdf.GenerateExpenseReport(model);
            },
            cancellationToken);

    public Task<ReportGenerationResult> GenerateProfitLossReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(
            ReportTypes.ProfitLoss,
            "Profit & Loss Statement",
            $"Profit-Loss-{DateTime.UtcNow:yyyyMMdd}.pdf",
            query,
            null,
            async ct =>
            {
                var header = await GetHeaderAsync(ct);
                var pnl = await _finance.GetProfitLossAsync(query.StartDate, query.EndDate, query.Period, "month", ct);
                var margin = pnl.TotalRevenue > 0
                    ? Math.Round(pnl.NetProfit / pnl.TotalRevenue * 100, 2)
                    : 0;

                var model = new ProfitLossPdfModel
                {
                    Header = header,
                    ReportDate = DateTime.UtcNow,
                    PeriodLabel = FormatPeriodLabel(pnl.StartDate, pnl.EndDate, pnl.Period),
                    TotalRevenue = pnl.TotalRevenue,
                    TotalExpenses = pnl.TotalExpenses,
                    NetProfit = pnl.NetProfit,
                    ProfitMarginPercent = margin,
                    PeriodBreakdown = pnl.PeriodBreakdown
                        .Select(p => new PdfTableRow
                        {
                            Cells = [p.Period, FormatMoney(p.Amount, header.Currency)]
                        })
                        .ToList()
                };

                return _pdf.GenerateProfitLossReport(model);
            },
            cancellationToken);

    public Task<ReportGenerationResult> GenerateCustomerReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(
            ReportTypes.Customers,
            "Customer Report",
            $"Customer-Report-{DateTime.UtcNow:yyyyMMdd}.pdf",
            query,
            query.CustomerId,
            async ct =>
            {
                var header = await GetHeaderAsync(ct);
                var range = await ResolveRangeAsync(query, ct);

                var customerQuery = _context.Customers.AsNoTracking().AsQueryable();
                if (query.CustomerId.HasValue)
                    customerQuery = customerQuery.Where(x => x.Id == query.CustomerId.Value);

                var customers = await customerQuery
                    .OrderBy(x => x.FirstName)
                    .ThenBy(x => x.LastName)
                    .Take(query.CustomerId.HasValue ? 1 : 100)
                    .Select(x => new
                    {
                        x.Id,
                        Name = x.FirstName + " " + x.LastName,
                        x.CreatedAt
                    })
                    .ToListAsync(ct);

                if (customers.Count == 0)
                    throw new NotFoundException("No customers found for this report.");

                var customerIds = customers.Select(x => x.Id).ToList();

                var projectCounts = await _context.Orders
                    .AsNoTracking()
                    .Where(x => customerIds.Contains(x.CustomerId))
                    .GroupBy(x => x.CustomerId)
                    .Select(g => new { CustomerId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CustomerId, x => x.Count, ct);

                var revenueByCustomer = await _context.Invoices
                    .AsNoTracking()
                    .Where(x => customerIds.Contains(x.CustomerId)
                        && x.InvoiceDate >= range.StartDate
                        && x.InvoiceDate <= range.EndDate
                        && !ExcludedInvoiceStatuses.Contains(x.Status))
                    .GroupBy(x => x.CustomerId)
                    .Select(g => new { CustomerId = g.Key, Revenue = g.Sum(x => x.GrandTotal) })
                    .ToDictionaryAsync(x => x.CustomerId, x => x.Revenue, ct);

                var outstandingByCustomer = await _context.Invoices
                    .AsNoTracking()
                    .Where(x => customerIds.Contains(x.CustomerId) && x.OutstandingAmount > 0)
                    .GroupBy(x => x.CustomerId)
                    .Select(g => new { CustomerId = g.Key, Outstanding = g.Sum(x => x.OutstandingAmount) })
                    .ToDictionaryAsync(x => x.CustomerId, x => x.Outstanding, ct);

                var rows = customers.Select(c => new PdfTableRow
                {
                    Cells =
                    [
                        c.Name,
                        projectCounts.GetValueOrDefault(c.Id, 0).ToString(),
                        FormatMoney(revenueByCustomer.GetValueOrDefault(c.Id, 0), header.Currency),
                        FormatMoney(outstandingByCustomer.GetValueOrDefault(c.Id, 0), header.Currency),
                        c.CreatedAt.ToString("yyyy-MM-dd")
                    ]
                }).ToList();

                var model = new CustomerReportPdfModel
                {
                    Header = header,
                    ReportDate = DateTime.UtcNow,
                    PeriodLabel = FormatPeriodLabel(range.StartDate, range.EndDate, range.Period),
                    Customers = rows
                };

                return _pdf.GenerateCustomerReport(model);
            },
            cancellationToken);

    public Task<ReportGenerationResult> GenerateProjectReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default)
    {
        if (!query.ProjectId.HasValue)
            throw new BadRequestException("ProjectId is required for project reports.");

        return GenerateAsync(
            ReportTypes.Projects,
            "Project Report",
            $"Project-Report-{DateTime.UtcNow:yyyyMMdd}.pdf",
            query,
            query.ProjectId,
            async ct =>
            {
                var header = await GetHeaderAsync(ct);
                var projectId = query.ProjectId!.Value;

                var order = await _context.Orders
                    .AsNoTracking()
                    .Where(x => x.Id == projectId)
                    .Select(x => new
                    {
                        x.OrderNumber,
                        x.Status,
                        x.OrderDate,
                        x.GrandTotal,
                        x.TotalAmount,
                        x.Discount,
                        x.Tax,
                        CustomerName = x.Customer.FirstName + " " + x.Customer.LastName
                    })
                    .FirstOrDefaultAsync(ct);

                if (order is null)
                    throw new NotFoundException("Project not found.");

                var tasks = await (
                    from item in _context.OrderItems.AsNoTracking()
                    join product in _context.Products.AsNoTracking() on item.ProductId equals product.Id into products
                    from product in products.DefaultIfEmpty()
                    where item.OrderId == projectId && !item.IsDeleted
                    select new
                    {
                        Name = product != null ? product.Name : "Line item",
                        item.Quantity,
                        item.UnitPrice,
                        item.Total
                    })
                    .ToListAsync(ct);

                var invoiceRevenue = await _context.Invoices
                    .AsNoTracking()
                    .Where(x => x.OrderId == projectId && !ExcludedInvoiceStatuses.Contains(x.Status))
                    .SumAsync(x => x.GrandTotal, ct);

                var projectInfo = new List<PdfKeyValueRow>
                {
                    new() { Label = "Project Number", Value = order.OrderNumber },
                    new() { Label = "Customer", Value = order.CustomerName },
                    new() { Label = "Status", Value = order.Status },
                    new() { Label = "Start Date", Value = order.OrderDate.ToString("yyyy-MM-dd") },
                    new() { Label = "Budget", Value = FormatMoney(order.GrandTotal, header.Currency) },
                    new() { Label = "Revenue", Value = FormatMoney(invoiceRevenue, header.Currency) }
                };

                var model = new ProjectReportPdfModel
                {
                    Header = header,
                    ReportDate = DateTime.UtcNow,
                    ProjectInfo = projectInfo,
                    AssignedMembers = [],
                    Tasks = tasks.Select(t => new PdfTableRow
                    {
                        Cells =
                        [
                            t.Name,
                            t.Quantity.ToString("N2"),
                            FormatMoney(t.UnitPrice, header.Currency),
                            FormatMoney(t.Total, header.Currency)
                        ]
                    }).ToList()
                };

                return _pdf.GenerateProjectReport(model);
            },
            cancellationToken);
    }

    public Task<ReportGenerationResult> GenerateTaskReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(
            ReportTypes.Tasks,
            "Task Productivity Report",
            $"Task-Report-{DateTime.UtcNow:yyyyMMdd}.pdf",
            query,
            null,
            async ct =>
            {
                var header = await GetHeaderAsync(ct);
                var taskAnalytics = await _analytics.GetTaskAnalyticsAsync(query.StartDate, query.EndDate, query.Period, ct);
                var breakdown = taskAnalytics.Breakdown;
                var completionRate = breakdown.Total > 0
                    ? Math.Round((decimal)breakdown.Completed / breakdown.Total * 100, 1)
                    : 0;

                var model = new TaskReportPdfModel
                {
                    Header = header,
                    ReportDate = DateTime.UtcNow,
                    PeriodLabel = FormatPeriodLabel(taskAnalytics.DateRange),
                    CompletedTasks = breakdown.Completed,
                    PendingTasks = breakdown.Pending,
                    OverdueTasks = breakdown.Overdue,
                    CompletionRate = completionRate
                };

                return _pdf.GenerateTaskReport(model);
            },
            cancellationToken);

    public Task<ReportGenerationResult> GenerateInvoicePdfAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        GenerateAsync(
            ReportTypes.Invoice,
            "Invoice",
            $"Invoice-{DateTime.UtcNow:yyyyMMdd}.pdf",
            new ReportQueryParams(),
            invoiceId,
            async ct =>
            {
                var header = await GetHeaderAsync(ct);

                var invoice = await _context.Invoices
                    .AsNoTracking()
                    .Where(x => x.Id == invoiceId)
                    .Select(x => new
                    {
                        x.InvoiceNumber,
                        x.InvoiceDate,
                        x.DueDate,
                        x.Status,
                        x.SubTotal,
                        x.Discount,
                        x.Tax,
                        x.GrandTotal,
                        x.OrderId,
                        x.Notes,
                        CustomerName = x.Customer.FirstName + " " + x.Customer.LastName,
                        CustomerEmail = x.Customer.Email,
                        CustomerAddress = x.Customer.Address + ", " + x.Customer.City + ", " + x.Customer.Country
                    })
                    .FirstOrDefaultAsync(ct);

                if (invoice is null)
                    throw new NotFoundException("Invoice not found.");

                var amountPaidByOrderId = await InvoicePaymentCalculator.GetAmountPaidByOrderIdsAsync(
                    _context,
                    [invoice.OrderId],
                    ct);

                var amountPaid = amountPaidByOrderId.TryGetValue(invoice.OrderId, out var paid)
                    ? Math.Round(paid, 2)
                    : 0;
                var outstanding = Math.Round(invoice.GrandTotal - amountPaid, 2);

                var lineItems = await (
                    from item in _context.OrderItems.AsNoTracking()
                    join product in _context.Products.AsNoTracking() on item.ProductId equals product.Id into products
                    from product in products.DefaultIfEmpty()
                    where item.OrderId == invoice.OrderId && !item.IsDeleted
                    select new InvoiceLineItem
                    {
                        Description = product != null ? product.Name : "Item",
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Total = item.Total
                    })
                    .ToListAsync(ct);

                var model = new InvoicePdfModel
                {
                    Header = header,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    DueDate = invoice.DueDate,
                    Status = invoice.Status,
                    CustomerName = invoice.CustomerName,
                    CustomerEmail = invoice.CustomerEmail,
                    CustomerAddress = invoice.CustomerAddress,
                    LineItems = lineItems,
                    SubTotal = invoice.SubTotal,
                    Discount = invoice.Discount,
                    Tax = invoice.Tax,
                    GrandTotal = invoice.GrandTotal,
                    AmountPaid = amountPaid,
                    OutstandingAmount = outstanding,
                    Notes = invoice.Notes
                };

                return _pdf.GenerateInvoice(model);
            },
            cancellationToken);

    public async Task<ReportHistoryResponse> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var items = await _context.GeneratedReports
            .AsNoTracking()
            .OrderByDescending(x => x.GeneratedAt)
            .Take(100)
            .Select(x => new ReportHistoryItemDto
            {
                Id = x.Id,
                ReportName = x.ReportName,
                ReportType = x.ReportType,
                GeneratedBy = x.GeneratedByName,
                GeneratedAt = x.GeneratedAt,
                FileType = x.FileType,
                FileName = x.FileName
            })
            .ToListAsync(cancellationToken);

        return new ReportHistoryResponse { Items = items };
    }

    public async Task<ReportGenerationResult> DownloadHistoryAsync(
        Guid historyId,
        CancellationToken cancellationToken = default)
    {
        var report = await _context.GeneratedReports
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == historyId, cancellationToken);

        if (report is null)
            throw new NotFoundException("Report not found.");

        if (report.FileContent is null || report.FileContent.Length == 0)
            return await RegenerateHistoryAsync(historyId, cancellationToken);

        return new ReportGenerationResult
        {
            Content = report.FileContent,
            FileName = report.FileName,
            ContentType = report.FileType,
            HistoryId = report.Id
        };
    }

    public async Task<ReportGenerationResult> RegenerateHistoryAsync(
        Guid historyId,
        CancellationToken cancellationToken = default)
    {
        var report = await _context.GeneratedReports
            .FirstOrDefaultAsync(x => x.Id == historyId, cancellationToken);

        if (report is null)
            throw new NotFoundException("Report not found.");

        var query = string.IsNullOrWhiteSpace(report.ParametersJson)
            ? new ReportQueryParams()
            : JsonSerializer.Deserialize<ReportQueryParams>(report.ParametersJson) ?? new ReportQueryParams();

        if (report.EntityId.HasValue)
        {
            if (report.ReportType == ReportTypes.Invoice)
                return await GenerateInvoicePdfAsync(report.EntityId.Value, cancellationToken);

            if (report.ReportType == ReportTypes.Projects)
            {
                query = new ReportQueryParams
                {
                    StartDate = query.StartDate,
                    EndDate = query.EndDate,
                    Period = query.Period,
                    ProjectId = report.EntityId
                };
            }

            if (report.ReportType == ReportTypes.Customers)
            {
                query = new ReportQueryParams
                {
                    StartDate = query.StartDate,
                    EndDate = query.EndDate,
                    Period = query.Period,
                    CustomerId = report.EntityId
                };
            }
        }

        var result = report.ReportType switch
        {
            ReportTypes.BusinessSummary => await GenerateBusinessSummaryAsync(query, cancellationToken),
            ReportTypes.Revenue => await GenerateRevenueReportAsync(query, cancellationToken),
            ReportTypes.Expenses => await GenerateExpenseReportAsync(query, cancellationToken),
            ReportTypes.ProfitLoss => await GenerateProfitLossReportAsync(query, cancellationToken),
            ReportTypes.Customers => await GenerateCustomerReportAsync(query, cancellationToken),
            ReportTypes.Projects => await GenerateProjectReportAsync(query, cancellationToken),
            ReportTypes.Tasks => await GenerateTaskReportAsync(query, cancellationToken),
            ReportTypes.Invoice when report.EntityId.HasValue =>
                await GenerateInvoicePdfAsync(report.EntityId.Value, cancellationToken),
            _ => throw new BadRequestException($"Cannot regenerate report type '{report.ReportType}'.")
        };

        return result;
    }

    private async Task<ReportGenerationResult> GenerateAsync(
        string reportType,
        string reportName,
        string fileName,
        ReportQueryParams query,
        Guid? entityId,
        Func<CancellationToken, Task<byte[]>> generatePdf,
        CancellationToken cancellationToken)
    {
        var content = await generatePdf(cancellationToken);
        var userId = _currentUser.UserId ?? "system";
        var userName = await GetCurrentUserNameAsync(cancellationToken);

        var history = new GeneratedReport
        {
            ReportName = reportName,
            ReportType = reportType,
            FileType = "application/pdf",
            FileName = fileName,
            GeneratedByUserId = userId,
            GeneratedByName = userName,
            GeneratedAt = DateTime.UtcNow,
            ParametersJson = JsonSerializer.Serialize(query),
            EntityId = entityId,
            FileContent = content
        };

        _context.GeneratedReports.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        return new ReportGenerationResult
        {
            Content = content,
            FileName = fileName,
            ContentType = "application/pdf",
            HistoryId = history.Id
        };
    }

    private async Task<BusinessHeaderInfo> GetHeaderAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId
            ?? throw new UnauthorizedException("Tenant context is required.");

        var tenant = await _context.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => new { x.Name, x.Address, x.Email, x.Phone, x.OwnerUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
            throw new NotFoundException("Business not found.");

        var settings = await _context.TenantSettings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.LogoUrl, x.Currency })
            .FirstOrDefaultAsync(cancellationToken);

        var owner = await _userManager.FindByIdAsync(tenant.OwnerUserId);
        var ownerName = owner is not null
            ? $"{owner.FirstName} {owner.LastName}".Trim()
            : "Owner";

        return new BusinessHeaderInfo
        {
            BusinessName = tenant.Name,
            LogoUrl = settings?.LogoUrl,
            OwnerName = ownerName,
            Address = tenant.Address,
            Email = tenant.Email,
            Phone = tenant.Phone,
            Currency = settings?.Currency ?? "USD"
        };
    }

    private async Task<string> GetCurrentUserNameAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
            return _currentUser.Email ?? "System";

        var user = await _userManager.FindByIdAsync(_currentUser.UserId);
        if (user is null)
            return _currentUser.Email ?? "User";

        return $"{user.FirstName} {user.LastName}".Trim();
    }

    private async Task<(DateTime StartDate, DateTime EndDate, string Period)> ResolveRangeAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken)
    {
        var overview = await _analytics.GetOverviewAsync(query.StartDate, query.EndDate, query.Period, cancellationToken);
        return (overview.DateRange.StartDate, overview.DateRange.EndDate, overview.DateRange.Period);
    }

    private static PdfChartSnapshot ToChartSnapshot(ChartDataResponse chart) =>
        ToChartSnapshot(chart.Title, chart);

    private static PdfChartSnapshot ToChartSnapshot(string title, ChartDataResponse chart) =>
        new()
        {
            Title = title,
            Labels = chart.Labels,
            Values = chart.Datasets.FirstOrDefault()?.Data ?? []
        };

    private static string FormatPeriodLabel(DashboardDateRangeInfo range) =>
        FormatPeriodLabel(range.StartDate, range.EndDate, range.Period);

    private static string FormatPeriodLabel(DateTime start, DateTime end, string period) =>
        $"{start:MMM dd, yyyy} – {end:MMM dd, yyyy} ({period})";

    private static string FormatMoney(decimal amount, string currency) =>
        currency switch
        {
            "EUR" => amount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("de-DE")),
            "GBP" => amount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-GB")),
            _ => amount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
        };
}

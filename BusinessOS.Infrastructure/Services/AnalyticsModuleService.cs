using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Analytics.DTOs;
using BusinessOS.Application.Features.Analytics.Models;
using BusinessOS.Application.Features.Analytics.Services;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

/// <summary>
/// Advanced analytics aggregations. Projects map to Orders; tasks map to Order line items.
/// </summary>
public sealed class AnalyticsModuleService : IAnalyticsModuleService
{
    private static readonly string[] ExcludedInvoiceStatuses =
    [
        InvoiceStatusNames.Draft,
        InvoiceStatusNames.Cancelled
    ];

    private static readonly string[] ActiveOrderStatuses =
    [
        OrderStatusNames.Confirmed,
        OrderStatusNames.Processing
    ];

    private static readonly string[] PendingOrderStatuses =
    [
        OrderStatusNames.Pending,
        OrderStatusNames.Confirmed,
        OrderStatusNames.Processing
    ];

    private readonly IApplicationDbContext _context;
    private readonly IAnalyticsDateRangeResolver _dateRangeResolver;

    public AnalyticsModuleService(
        IApplicationDbContext context,
        IAnalyticsDateRangeResolver dateRangeResolver)
    {
        _context = context;
        _dateRangeResolver = dateRangeResolver;
    }

    public async Task<AnalyticsOverviewResponse> GetOverviewAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);
        var previous = GetPreviousPeriod(range);

        var currentCustomers = await CountCustomersAsync(range, cancellationToken);
        var previousCustomers = await CountCustomersAsync(previous, cancellationToken);

        var currentActiveProjects = await CountActiveProjectsAsync(range, cancellationToken);
        var previousActiveProjects = await CountActiveProjectsAsync(previous, cancellationToken);

        var currentTasks = await CountTasksAsync(range, null, cancellationToken);
        var previousTasks = await CountTasksAsync(previous, null, cancellationToken);

        var currentCompletedTasks = await CountTasksAsync(range, OrderStatusNames.Completed, cancellationToken);
        var previousCompletedTasks = await CountTasksAsync(previous, OrderStatusNames.Completed, cancellationToken);

        var currentRevenue = await SumInvoiceRevenueAsync(range, cancellationToken);
        var previousRevenue = await SumInvoiceRevenueAsync(previous, cancellationToken);

        var currentExpenses = await SumExpensesAsync(range, cancellationToken);
        var previousExpenses = await SumExpensesAsync(previous, cancellationToken);

        var currentProfit = currentRevenue - currentExpenses;
        var previousProfit = previousRevenue - previousExpenses;

        var currentInvoices = await CountInvoicesAsync(range, cancellationToken);
        var previousInvoices = await CountInvoicesAsync(previous, cancellationToken);

        return new AnalyticsOverviewResponse
        {
            TotalCustomers = BuildMetric(currentCustomers, previousCustomers),
            ActiveProjects = BuildMetric(currentActiveProjects, previousActiveProjects),
            TotalTasks = BuildMetric(currentTasks, previousTasks),
            CompletedTasks = BuildMetric(currentCompletedTasks, previousCompletedTasks),
            TotalRevenue = BuildMetric(currentRevenue, previousRevenue),
            TotalExpenses = BuildMetric(currentExpenses, previousExpenses),
            NetProfit = BuildMetric(currentProfit, previousProfit),
            TotalInvoices = BuildMetric(currentInvoices, previousInvoices),
            DateRange = MapDateRange(range)
        };
    }

    public async Task<ChartDataResponse> GetRevenueChartAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);
        var chartRange = GetLast12MonthsRange(range.EndDate);
        var months = BuildMonthSequence(chartRange.EndDate, 12);

        var revenueByMonth = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.InvoiceDate >= chartRange.StartDate
                && x.InvoiceDate <= chartRange.EndDate
                && !ExcludedInvoiceStatuses.Contains(x.Status))
            .GroupBy(x => new { x.InvoiceDate.Year, x.InvoiceDate.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(x => x.GrandTotal)
            })
            .ToListAsync(cancellationToken);

        var lookup = revenueByMonth.ToDictionary(
            x => new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            x => x.Revenue);

        return new ChartDataResponse
        {
            ChartType = "line",
            Title = "Monthly Revenue",
            Labels = months.Select(x => x.ToString("MMM yyyy")).ToList(),
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "Revenue",
                    Data = months.Select(m => lookup.GetValueOrDefault(m, 0)).ToList(),
                    ChartStyle = "line"
                }
            ],
            DateRange = MapDateRange(range)
        };
    }

    public async Task<ChartDataResponse> GetExpenseChartAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);
        var chartRange = GetLast12MonthsRange(range.EndDate);
        var months = BuildMonthSequence(chartRange.EndDate, 12);

        var expensesByMonth = await _context.Expenses
            .AsNoTracking()
            .Where(x => x.ExpenseDate >= chartRange.StartDate && x.ExpenseDate <= chartRange.EndDate)
            .GroupBy(x => new { x.ExpenseDate.Year, x.ExpenseDate.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Total = g.Sum(x => x.Amount)
            })
            .ToListAsync(cancellationToken);

        var lookup = expensesByMonth.ToDictionary(
            x => new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            x => x.Total);

        return new ChartDataResponse
        {
            ChartType = "bar",
            Title = "Monthly Expenses",
            Labels = months.Select(x => x.ToString("MMM yyyy")).ToList(),
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "Expenses",
                    Data = months.Select(m => lookup.GetValueOrDefault(m, 0)).ToList(),
                    ChartStyle = "bar"
                }
            ],
            DateRange = MapDateRange(range)
        };
    }

    public async Task<ChartDataResponse> GetProfitChartAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);
        var chartRange = GetLast12MonthsRange(range.EndDate);
        var months = BuildMonthSequence(chartRange.EndDate, 12);

        var revenueByMonth = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.InvoiceDate >= chartRange.StartDate
                && x.InvoiceDate <= chartRange.EndDate
                && !ExcludedInvoiceStatuses.Contains(x.Status))
            .GroupBy(x => new { x.InvoiceDate.Year, x.InvoiceDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.GrandTotal) })
            .ToListAsync(cancellationToken);

        var expensesByMonth = await _context.Expenses
            .AsNoTracking()
            .Where(x => x.ExpenseDate >= chartRange.StartDate && x.ExpenseDate <= chartRange.EndDate)
            .GroupBy(x => new { x.ExpenseDate.Year, x.ExpenseDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var revenueLookup = revenueByMonth.ToDictionary(
            x => new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            x => x.Total);
        var expenseLookup = expensesByMonth.ToDictionary(
            x => new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            x => x.Total);

        var revenueData = months.Select(m => revenueLookup.GetValueOrDefault(m, 0)).ToList();
        var expenseData = months.Select(m => expenseLookup.GetValueOrDefault(m, 0)).ToList();
        var profitData = months
            .Select((m, i) => revenueData[i] - expenseData[i])
            .ToList();

        return new ChartDataResponse
        {
            ChartType = "line",
            Title = "Revenue vs Expenses vs Profit",
            Labels = months.Select(x => x.ToString("MMM yyyy")).ToList(),
            Datasets =
            [
                new ChartDatasetDto { Label = "Revenue", Data = revenueData, ChartStyle = "line" },
                new ChartDatasetDto { Label = "Expenses", Data = expenseData, ChartStyle = "bar" },
                new ChartDatasetDto { Label = "Profit", Data = profitData, ChartStyle = "line" }
            ],
            DateRange = MapDateRange(range)
        };
    }

    public async Task<ChartDataResponse> GetCustomerGrowthChartAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);
        var chartRange = GetLast12MonthsRange(range.EndDate);
        var months = BuildMonthSequence(chartRange.EndDate, 12);

        var customersByMonth = await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= chartRange.StartDate && x.CreatedAt <= chartRange.EndDate)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var lookup = customersByMonth.ToDictionary(
            x => new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            x => (decimal)x.Count);

        return new ChartDataResponse
        {
            ChartType = "area",
            Title = "Customer Growth",
            Labels = months.Select(x => x.ToString("MMM yyyy")).ToList(),
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "New Customers",
                    Data = months.Select(m => lookup.GetValueOrDefault(m, 0)).ToList(),
                    ChartStyle = "area"
                }
            ],
            DateRange = MapDateRange(range)
        };
    }

    public async Task<AnalyticsProjectAnalyticsResponse> GetProjectAnalyticsAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);

        var statusCounts = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= range.StartDate && x.OrderDate <= range.EndDate)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var breakdown = new AnalyticsProjectStatusDto
        {
            Active = statusCounts
                .Where(x => ActiveOrderStatuses.Contains(x.Status))
                .Sum(x => x.Count),
            Completed = statusCounts
                .FirstOrDefault(x => x.Status == OrderStatusNames.Completed)?.Count ?? 0,
            OnHold = statusCounts
                .FirstOrDefault(x => x.Status == OrderStatusNames.Pending)?.Count ?? 0,
            Cancelled = statusCounts
                .FirstOrDefault(x => x.Status == OrderStatusNames.Cancelled)?.Count ?? 0
        };

        var chart = new ChartDataResponse
        {
            ChartType = "doughnut",
            Title = "Project Status",
            Labels = ["Active", "Completed", "On Hold", "Cancelled"],
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "Projects",
                    Data =
                    [
                        breakdown.Active,
                        breakdown.Completed,
                        breakdown.OnHold,
                        breakdown.Cancelled
                    ],
                    ChartStyle = "doughnut"
                }
            ],
            DateRange = MapDateRange(range)
        };

        return new AnalyticsProjectAnalyticsResponse
        {
            StatusBreakdown = breakdown,
            Chart = chart,
            DateRange = MapDateRange(range)
        };
    }

    public async Task<AnalyticsTaskAnalyticsResponse> GetTaskAnalyticsAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);

        var taskStats = await (
            from item in _context.OrderItems.AsNoTracking()
            join order in _context.Orders.AsNoTracking() on item.OrderId equals order.Id
            where !item.IsDeleted
                && order.OrderDate >= range.StartDate
                && order.OrderDate <= range.EndDate
            select new
            {
                order.Status,
                IsOverdue = _context.Invoices.Any(i =>
                    i.OrderId == order.Id && i.Status == InvoiceStatusNames.Overdue)
            })
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(x => x.Status == OrderStatusNames.Completed),
                Pending = g.Count(x => PendingOrderStatuses.Contains(x.Status)),
                Overdue = g.Count(x => x.IsOverdue)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var breakdown = new AnalyticsTaskBreakdownDto
        {
            Total = taskStats?.Total ?? 0,
            Completed = taskStats?.Completed ?? 0,
            Pending = taskStats?.Pending ?? 0,
            Overdue = taskStats?.Overdue ?? 0
        };

        var chart = new ChartDataResponse
        {
            ChartType = "doughnut",
            Title = "Task Analytics",
            Labels = ["Completed", "Pending", "Overdue"],
            Datasets =
            [
                new ChartDatasetDto
                {
                    Label = "Tasks",
                    Data =
                    [
                        breakdown.Completed,
                        breakdown.Pending,
                        breakdown.Overdue
                    ],
                    ChartStyle = "doughnut"
                }
            ],
            DateRange = MapDateRange(range)
        };

        return new AnalyticsTaskAnalyticsResponse
        {
            Breakdown = breakdown,
            Chart = chart,
            DateRange = MapDateRange(range)
        };
    }

    public async Task<AnalyticsTopCustomersResponse> GetTopCustomersAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int top = 10,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);
        var limit = Math.Clamp(top, 1, 50);

        var invoiceStats = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.InvoiceDate >= range.StartDate
                && x.InvoiceDate <= range.EndDate
                && !ExcludedInvoiceStatuses.Contains(x.Status))
            .GroupBy(x => x.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Revenue = g.Sum(x => x.GrandTotal),
                InvoiceCount = g.Count()
            })
            .OrderByDescending(x => x.Revenue)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (invoiceStats.Count == 0)
        {
            return new AnalyticsTopCustomersResponse
            {
                Customers = [],
                DateRange = MapDateRange(range)
            };
        }

        var customerIds = invoiceStats.Select(x => x.CustomerId).ToList();

        var customerNames = await _context.Customers
            .AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .Select(x => new { x.Id, Name = x.FirstName + " " + x.LastName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var projectCounts = await _context.Orders
            .AsNoTracking()
            .Where(x => customerIds.Contains(x.CustomerId)
                && x.OrderDate >= range.StartDate
                && x.OrderDate <= range.EndDate)
            .GroupBy(x => x.CustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Count, cancellationToken);

        var customers = invoiceStats
            .Select(x => new AnalyticsTopCustomerDto
            {
                CustomerId = x.CustomerId,
                CustomerName = customerNames.GetValueOrDefault(x.CustomerId, "Unknown"),
                RevenueGenerated = Math.Round(x.Revenue, 2),
                ProjectsCount = projectCounts.GetValueOrDefault(x.CustomerId, 0),
                InvoicesCount = x.InvoiceCount
            })
            .ToList();

        return new AnalyticsTopCustomersResponse
        {
            Customers = customers,
            DateRange = MapDateRange(range)
        };
    }

    public async Task<AnalyticsRecentActivityResponse> GetRecentActivityAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);
        var take = Math.Clamp(limit, 1, 100);

        var customerActivities = await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= range.StartDate && x.CreatedAt <= range.EndDate)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new AnalyticsRecentActivityDto
            {
                Id = x.Id,
                ActivityType = "Customer",
                Title = "New Customer",
                Description = x.FirstName + " " + x.LastName,
                OccurredAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var projectActivities = await _context.Orders
            .AsNoTracking()
            .Where(x => x.CreatedAt >= range.StartDate && x.CreatedAt <= range.EndDate)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new AnalyticsRecentActivityDto
            {
                Id = x.Id,
                ActivityType = "Project",
                Title = "New Project",
                Description = x.OrderNumber,
                OccurredAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var taskActivities = await (
            from item in _context.OrderItems.AsNoTracking()
            join order in _context.Orders.AsNoTracking() on item.OrderId equals order.Id
            join product in _context.Products.AsNoTracking() on item.ProductId equals product.Id into products
            from product in products.DefaultIfEmpty()
            where !item.IsDeleted
                && order.CreatedAt >= range.StartDate
                && order.CreatedAt <= range.EndDate
            orderby order.CreatedAt descending
            select new AnalyticsRecentActivityDto
            {
                Id = item.Id,
                ActivityType = "Task",
                Title = "New Task",
                Description = product != null ? product.Name : "Order line item",
                OccurredAt = order.CreatedAt
            })
            .Take(take)
            .ToListAsync(cancellationToken);

        var invoiceActivities = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.CreatedAt >= range.StartDate && x.CreatedAt <= range.EndDate)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new AnalyticsRecentActivityDto
            {
                Id = x.Id,
                ActivityType = "Invoice",
                Title = "New Invoice",
                Description = x.InvoiceNumber,
                OccurredAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var activities = customerActivities
            .Concat(projectActivities)
            .Concat(taskActivities)
            .Concat(invoiceActivities)
            .OrderByDescending(x => x.OccurredAt)
            .Take(take)
            .ToList();

        return new AnalyticsRecentActivityResponse
        {
            Activities = activities,
            DateRange = MapDateRange(range)
        };
    }

    private async Task<decimal> CountCustomersAsync(
        AnalyticsDateRange range,
        CancellationToken cancellationToken) =>
        await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= range.StartDate && x.CreatedAt <= range.EndDate)
            .CountAsync(cancellationToken);

    private async Task<decimal> CountActiveProjectsAsync(
        AnalyticsDateRange range,
        CancellationToken cancellationToken) =>
        await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= range.StartDate
                && x.OrderDate <= range.EndDate
                && ActiveOrderStatuses.Contains(x.Status))
            .CountAsync(cancellationToken);

    private async Task<decimal> CountTasksAsync(
        AnalyticsDateRange range,
        string? orderStatus,
        CancellationToken cancellationToken) =>
        await (
            from item in _context.OrderItems.AsNoTracking()
            join order in _context.Orders.AsNoTracking() on item.OrderId equals order.Id
            where !item.IsDeleted
                && order.OrderDate >= range.StartDate
                && order.OrderDate <= range.EndDate
                && (orderStatus == null || order.Status == orderStatus)
            select item)
            .CountAsync(cancellationToken);

    private async Task<decimal> SumInvoiceRevenueAsync(
        AnalyticsDateRange range,
        CancellationToken cancellationToken) =>
        await _context.Invoices
            .AsNoTracking()
            .Where(x => x.InvoiceDate >= range.StartDate
                && x.InvoiceDate <= range.EndDate
                && !ExcludedInvoiceStatuses.Contains(x.Status))
            .SumAsync(x => x.GrandTotal, cancellationToken);

    private async Task<decimal> SumExpensesAsync(
        AnalyticsDateRange range,
        CancellationToken cancellationToken) =>
        await _context.Expenses
            .AsNoTracking()
            .Where(x => x.ExpenseDate >= range.StartDate && x.ExpenseDate <= range.EndDate)
            .SumAsync(x => x.Amount, cancellationToken);

    private async Task<decimal> CountInvoicesAsync(
        AnalyticsDateRange range,
        CancellationToken cancellationToken) =>
        await _context.Invoices
            .AsNoTracking()
            .Where(x => x.InvoiceDate >= range.StartDate
                && x.InvoiceDate <= range.EndDate
                && !ExcludedInvoiceStatuses.Contains(x.Status))
            .CountAsync(cancellationToken);

    private static AnalyticsDateRange GetPreviousPeriod(AnalyticsDateRange range)
    {
        var duration = range.EndDate - range.StartDate;
        var previousEnd = range.StartDate.AddTicks(-1);
        var previousStart = previousEnd - duration;
        return new AnalyticsDateRange(previousStart, previousEnd, range.Period);
    }

    private static AnalyticsDateRange GetLast12MonthsRange(DateTime endDate)
    {
        var end = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
        var startMonth = new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
        return new AnalyticsDateRange(startMonth, end, "12months");
    }

    private static List<DateTime> BuildMonthSequence(DateTime endDate, int monthCount)
    {
        var endMonth = new DateTime(endDate.Year, endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var months = new List<DateTime>(monthCount);
        for (var i = monthCount - 1; i >= 0; i--)
            months.Add(endMonth.AddMonths(-i));
        return months;
    }

    private static AnalyticsMetricDto BuildMetric(decimal current, decimal previous) =>
        new()
        {
            Value = Math.Round(current, 2),
            PreviousValue = Math.Round(previous, 2),
            GrowthPercentage = CalculateGrowth(current, previous)
        };

    private static decimal CalculateGrowth(decimal current, decimal previous)
    {
        if (previous == 0)
            return current > 0 ? 100 : 0;

        return Math.Round((current - previous) / previous * 100, 2);
    }

    private static DashboardDateRangeInfo MapDateRange(AnalyticsDateRange range) =>
        new()
        {
            StartDate = range.StartDate,
            EndDate = range.EndDate,
            Period = range.Period
        };
}

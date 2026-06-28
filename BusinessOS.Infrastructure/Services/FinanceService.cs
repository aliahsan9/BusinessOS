using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Finance.DTOs;
using BusinessOS.Application.Features.Finance.Services;
using BusinessOS.Application.Features.Dashboard.Models;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class FinanceService : IFinanceService
{
    private readonly IApplicationDbContext _context;
    private readonly IDashboardDateRangeResolver _dateRangeResolver;

    public FinanceService(
        IApplicationDbContext context,
        IDashboardDateRangeResolver dateRangeResolver)
    {
        _context = context;
        _dateRangeResolver = dateRangeResolver;
    }

    public async Task<FinancialDashboardResponse> GetDashboardAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);

        var orderRevenue = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= range.StartDate
                && x.OrderDate <= range.EndDate
                && x.Status == OrderStatusNames.Completed)
            .SumAsync(x => x.GrandTotal, cancellationToken);

        var completedOrders = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= range.StartDate
                && x.OrderDate <= range.EndDate
                && x.Status == OrderStatusNames.Completed)
            .CountAsync(cancellationToken);

        var totalPayments = await _context.Payments
            .AsNoTracking()
            .Where(x => x.PaymentDate >= range.StartDate && x.PaymentDate <= range.EndDate)
            .SumAsync(x => x.Amount, cancellationToken);

        var expenseStats = await _context.Expenses
            .AsNoTracking()
            .Where(x => x.ExpenseDate >= range.StartDate && x.ExpenseDate <= range.EndDate)
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Sum(x => x.Amount), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        var outstandingInvoices = await _context.Invoices
            .AsNoTracking()
            .Where(x => x.OutstandingAmount > 0)
            .SumAsync(x => x.OutstandingAmount, cancellationToken);

        var totalExpenses = expenseStats?.Total ?? 0;
        var netProfit = orderRevenue - totalExpenses;

        var revenueTrend = await BuildOrderRevenueTrendAsync(range, cancellationToken);
        var expenseTrend = await BuildExpenseTrendAsync(range, cancellationToken);

        return new FinancialDashboardResponse
        {
            TotalRevenue = Math.Round(orderRevenue, 2),
            TotalExpenses = Math.Round(totalExpenses, 2),
            NetProfit = Math.Round(netProfit, 2),
            TotalPayments = Math.Round(totalPayments, 2),
            OutstandingInvoices = Math.Round(outstandingInvoices, 2),
            CompletedOrders = completedOrders,
            TotalExpensesCount = expenseStats?.Count ?? 0,
            Period = range.Period,
            StartDate = range.StartDate,
            EndDate = range.EndDate,
            RevenueTrend = revenueTrend,
            ExpenseTrend = expenseTrend
        };
    }

    public async Task<ProfitLossResponse> GetProfitLossAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        string? groupBy,
        CancellationToken cancellationToken = default)
    {
        var normalizedGroupBy = NormalizeGroupBy(groupBy);
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);

        var totalRevenue = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= range.StartDate
                && x.OrderDate <= range.EndDate
                && x.Status == OrderStatusNames.Completed)
            .SumAsync(x => x.GrandTotal, cancellationToken);

        var totalExpenses = await _context.Expenses
            .AsNoTracking()
            .Where(x => x.ExpenseDate >= range.StartDate && x.ExpenseDate <= range.EndDate)
            .SumAsync(x => x.Amount, cancellationToken);

        var grossProfit = totalRevenue;
        var netProfit = totalRevenue - totalExpenses;

        var periodBreakdown = normalizedGroupBy == "year"
            ? await BuildYearlyProfitLossAsync(range, cancellationToken)
            : await BuildMonthlyProfitLossAsync(range, cancellationToken);

        return new ProfitLossResponse
        {
            TotalRevenue = Math.Round(totalRevenue, 2),
            TotalExpenses = Math.Round(totalExpenses, 2),
            GrossProfit = Math.Round(grossProfit, 2),
            NetProfit = Math.Round(netProfit, 2),
            GroupBy = normalizedGroupBy,
            Period = range.Period,
            StartDate = range.StartDate,
            EndDate = range.EndDate,
            PeriodBreakdown = periodBreakdown
        };
    }

    public async Task<RevenueBreakdown> GetRevenueBreakdownAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);

        var orderRevenue = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= range.StartDate
                && x.OrderDate <= range.EndDate
                && x.Status == OrderStatusNames.Completed)
            .SumAsync(x => x.GrandTotal, cancellationToken);

        var paymentTotal = await _context.Payments
            .AsNoTracking()
            .Where(x => x.PaymentDate >= range.StartDate && x.PaymentDate <= range.EndDate)
            .SumAsync(x => x.Amount, cancellationToken);

        var byPaymentMethod = await _context.Payments
            .AsNoTracking()
            .Where(x => x.PaymentDate >= range.StartDate && x.PaymentDate <= range.EndDate)
            .GroupBy(x => x.PaymentMethod)
            .Select(g => new RevenueCategoryItem
            {
                Category = g.Key,
                Amount = Math.Round(g.Sum(x => x.Amount), 2),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(cancellationToken);

        var trends = await BuildOrderRevenueTrendAsync(range, cancellationToken);

        return new RevenueBreakdown
        {
            OrderRevenue = Math.Round(orderRevenue, 2),
            PaymentTotal = Math.Round(paymentTotal, 2),
            ByPaymentMethod = byPaymentMethod,
            Trends = trends,
            Period = range.Period,
            StartDate = range.StartDate,
            EndDate = range.EndDate
        };
    }

    public async Task<ExpenseBreakdown> GetExpenseBreakdownAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var range = _dateRangeResolver.Resolve(startDate, endDate, period);

        var expenses = _context.Expenses
            .AsNoTracking()
            .Where(x => x.ExpenseDate >= range.StartDate && x.ExpenseDate <= range.EndDate);

        var totalExpenses = await expenses.SumAsync(x => x.Amount, cancellationToken);
        var totalCount = await expenses.CountAsync(cancellationToken);

        var byCategoryRaw = await expenses
            .GroupBy(x => x.ExpenseCategory.Name)
            .Select(g => new
            {
                CategoryName = g.Key,
                Amount = g.Sum(x => x.Amount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(cancellationToken);

        var byCategory = byCategoryRaw
            .Select(x => new ExpenseCategoryItem
            {
                CategoryName = x.CategoryName,
                Amount = Math.Round(x.Amount, 2),
                Count = x.Count,
                Percentage = totalExpenses > 0
                    ? Math.Round(x.Amount / totalExpenses * 100, 2)
                    : 0
            })
            .ToList();

        var trends = await BuildExpenseTrendAsync(range, cancellationToken);

        return new ExpenseBreakdown
        {
            TotalExpenses = Math.Round(totalExpenses, 2),
            TotalCount = totalCount,
            ByCategory = byCategory,
            Trends = trends,
            Period = range.Period,
            StartDate = range.StartDate,
            EndDate = range.EndDate
        };
    }

    private async Task<IReadOnlyList<TrendPoint>> BuildMonthlyProfitLossAsync(
        DashboardDateRange range,
        CancellationToken cancellationToken)
    {
        var revenueByMonth = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= range.StartDate
                && x.OrderDate <= range.EndDate
                && x.Status == OrderStatusNames.Completed)
            .GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Amount = g.Sum(x => x.GrandTotal),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var expenseByMonth = await _context.Expenses
            .AsNoTracking()
            .Where(x => x.ExpenseDate >= range.StartDate && x.ExpenseDate <= range.EndDate)
            .GroupBy(x => new { x.ExpenseDate.Year, x.ExpenseDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Amount = g.Sum(x => x.Amount)
            })
            .ToListAsync(cancellationToken);

        var periods = revenueByMonth
            .Select(x => (x.Year, x.Month))
            .Union(expenseByMonth.Select(x => (x.Year, x.Month)))
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month);

        return periods
            .Select(p =>
            {
                var revenue = revenueByMonth
                    .FirstOrDefault(x => x.Year == p.Year && x.Month == p.Month);
                var expense = expenseByMonth
                    .FirstOrDefault(x => x.Year == p.Year && x.Month == p.Month);
                var net = (revenue?.Amount ?? 0) - (expense?.Amount ?? 0);

                return new TrendPoint
                {
                    Period = $"{p.Year}-{p.Month:D2}",
                    Amount = Math.Round(net, 2),
                    Count = revenue?.Count ?? 0
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<TrendPoint>> BuildYearlyProfitLossAsync(
        DashboardDateRange range,
        CancellationToken cancellationToken)
    {
        var revenueByYear = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= range.StartDate
                && x.OrderDate <= range.EndDate
                && x.Status == OrderStatusNames.Completed)
            .GroupBy(x => x.OrderDate.Year)
            .Select(g => new { Year = g.Key, Amount = g.Sum(x => x.GrandTotal), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var expenseByYear = await _context.Expenses
            .AsNoTracking()
            .Where(x => x.ExpenseDate >= range.StartDate && x.ExpenseDate <= range.EndDate)
            .GroupBy(x => x.ExpenseDate.Year)
            .Select(g => new { Year = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var periods = revenueByYear
            .Select(x => x.Year)
            .Union(expenseByYear.Select(x => x.Year))
            .OrderBy(x => x);

        return periods
            .Select(year =>
            {
                var revenue = revenueByYear.FirstOrDefault(x => x.Year == year);
                var expense = expenseByYear.FirstOrDefault(x => x.Year == year);
                var net = (revenue?.Amount ?? 0) - (expense?.Amount ?? 0);

                return new TrendPoint
                {
                    Period = year.ToString(),
                    Amount = Math.Round(net, 2),
                    Count = revenue?.Count ?? 0
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<TrendPoint>> BuildOrderRevenueTrendAsync(
        DashboardDateRange range,
        CancellationToken cancellationToken)
    {
        var rows = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= range.StartDate
                && x.OrderDate <= range.EndDate
                && x.Status == OrderStatusNames.Completed)
            .GroupBy(x => new { x.OrderDate.Year, x.OrderDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Amount = g.Sum(x => x.GrandTotal),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => new TrendPoint
            {
                Period = $"{x.Year}-{x.Month:D2}",
                Amount = Math.Round(x.Amount, 2),
                Count = x.Count
            })
            .ToList();
    }

    private async Task<IReadOnlyList<TrendPoint>> BuildExpenseTrendAsync(
        DashboardDateRange range,
        CancellationToken cancellationToken)
    {
        var rows = await _context.Expenses
            .AsNoTracking()
            .Where(x => x.ExpenseDate >= range.StartDate && x.ExpenseDate <= range.EndDate)
            .GroupBy(x => new { x.ExpenseDate.Year, x.ExpenseDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Amount = g.Sum(x => x.Amount),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => new TrendPoint
            {
                Period = $"{x.Year}-{x.Month:D2}",
                Amount = Math.Round(x.Amount, 2),
                Count = x.Count
            })
            .ToList();
    }

    private static string NormalizeGroupBy(string? groupBy)
    {
        var normalized = string.IsNullOrWhiteSpace(groupBy)
            ? "month"
            : groupBy.Trim().ToLowerInvariant();

        if (normalized is not ("month" or "year"))
            throw new BadRequestException("groupBy must be 'month' or 'year'.");

        return normalized;
    }
}

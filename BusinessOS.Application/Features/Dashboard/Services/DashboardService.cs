using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Dashboard.Models;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Dashboard.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserAnalyticsService _userAnalyticsService;

    public DashboardService(
        IApplicationDbContext context,
        IUserAnalyticsService userAnalyticsService)
    {
        _context = context;
        _userAnalyticsService = userAnalyticsService;
    }

    public async Task<DashboardOverviewResponse> GetOverviewAsync(
        DashboardDateRange dateRange,
        CancellationToken cancellationToken = default)
    {
        var previous = PreviousRange(dateRange);

        // Catalog totals — owners expect "how many customers/products we have", not
        // "how many were created in this period" (that caused zero-customer KPIs).
        var totalProducts = await _context.Products.AsNoTracking().CountAsync(cancellationToken);
        var totalCategories = await _context.Categories.AsNoTracking().CountAsync(cancellationToken);
        var totalCustomers = await _context.Customers.AsNoTracking().CountAsync(cancellationToken);

        var newCustomers = await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= dateRange.StartDate && x.CreatedAt <= dateRange.EndDate)
            .CountAsync(cancellationToken);

        var activeCustomers = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= dateRange.StartDate
                && x.OrderDate <= dateRange.EndDate
                && x.Status == OrderStatusNames.Completed)
            .Select(x => x.CustomerId)
            .Distinct()
            .CountAsync(cancellationToken);

        var orderStats = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= dateRange.StartDate && x.OrderDate <= dateRange.EndDate)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                TotalRevenue = g.Where(x => x.Status == OrderStatusNames.Completed)
                    .Sum(x => x.GrandTotal)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var previousOrderStats = await _context.Orders
            .AsNoTracking()
            .Where(x => x.OrderDate >= previous.StartDate && x.OrderDate <= previous.EndDate)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                TotalRevenue = g.Where(x => x.Status == OrderStatusNames.Completed)
                    .Sum(x => x.GrandTotal)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var inventoryStats = await _context.Inventories
            .AsNoTracking()
            .Select(x => new
            {
                x.CurrentStock,
                x.ReorderLevel,
                Value = x.CurrentStock * x.Product.CostPrice
            })
            .ToListAsync(cancellationToken);

        var totalActiveUsers = await _userAnalyticsService.GetActiveUserCountAsync(cancellationToken);
        var currency = await _context.Tenants
            .AsNoTracking()
            .Select(x => x.Currency)
            .FirstOrDefaultAsync(cancellationToken) ?? "USD";

        var revenue = Math.Round(orderStats?.TotalRevenue ?? 0, 2);
        var previousRevenue = Math.Round(previousOrderStats?.TotalRevenue ?? 0, 2);
        var orders = orderStats?.TotalOrders ?? 0;
        var previousOrders = previousOrderStats?.TotalOrders ?? 0;

        return new DashboardOverviewResponse
        {
            TotalProducts = totalProducts,
            TotalCategories = totalCategories,
            TotalCustomers = totalCustomers,
            NewCustomers = newCustomers,
            ActiveCustomers = activeCustomers,
            TotalOrders = orders,
            TotalRevenue = revenue,
            PreviousRevenue = previousRevenue,
            RevenueChangePercent = ChangePercent(revenue, previousRevenue),
            PreviousOrders = previousOrders,
            OrdersChangePercent = ChangePercent(orders, previousOrders),
            TotalInventoryValue = Math.Round(inventoryStats.Sum(x => x.Value), 2),
            TotalActiveUsers = totalActiveUsers,
            LowStockProducts = inventoryStats.Count(x => x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel),
            OutOfStockProducts = inventoryStats.Count(x => x.CurrentStock <= 0),
            Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
            DateRange = MapDateRange(dateRange),
            PreviousDateRange = MapDateRange(previous)
        };
    }

    /// <summary>Equivalent-length window immediately before the selected range.</summary>
    internal static DashboardDateRange PreviousRange(DashboardDateRange current)
    {
        var length = current.EndDate - current.StartDate;
        if (length < TimeSpan.Zero) length = TimeSpan.Zero;
        var previousEnd = current.StartDate.AddTicks(-1);
        var previousStart = previousEnd - length;
        return new DashboardDateRange(previousStart, previousEnd, current.Period);
    }

    /// <summary>Null when previous is zero to avoid misleading ∞% growth.</summary>
    internal static decimal? ChangePercent(decimal current, decimal previous)
    {
        if (previous == 0) return null;
        return Math.Round((current - previous) / previous * 100m, 1);
    }

    internal static decimal? ChangePercent(int current, int previous) =>
        ChangePercent((decimal)current, previous);

    internal static DashboardDateRangeInfo MapDateRange(DashboardDateRange dateRange) =>
        new()
        {
            StartDate = dateRange.StartDate,
            EndDate = dateRange.EndDate,
            Period = dateRange.Period
        };
}

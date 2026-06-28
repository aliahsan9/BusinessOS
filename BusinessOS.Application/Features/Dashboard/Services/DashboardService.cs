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
        var totalProducts = await _context.Products
            .AsNoTracking()
            .Where(x => x.CreatedAt >= dateRange.StartDate && x.CreatedAt <= dateRange.EndDate)
            .CountAsync(cancellationToken);

        var totalCategories = await _context.Categories
            .AsNoTracking()
            .Where(x => x.CreatedAt >= dateRange.StartDate && x.CreatedAt <= dateRange.EndDate)
            .CountAsync(cancellationToken);

        var totalCustomers = await _context.Customers
            .AsNoTracking()
            .Where(x => x.CreatedAt >= dateRange.StartDate && x.CreatedAt <= dateRange.EndDate)
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

        return new DashboardOverviewResponse
        {
            TotalProducts = totalProducts,
            TotalCategories = totalCategories,
            TotalCustomers = totalCustomers,
            TotalOrders = orderStats?.TotalOrders ?? 0,
            TotalRevenue = Math.Round(orderStats?.TotalRevenue ?? 0, 2),
            TotalInventoryValue = Math.Round(inventoryStats.Sum(x => x.Value), 2),
            TotalActiveUsers = totalActiveUsers,
            LowStockProducts = inventoryStats.Count(x => x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel),
            OutOfStockProducts = inventoryStats.Count(x => x.CurrentStock <= 0),
            DateRange = MapDateRange(dateRange)
        };
    }

    internal static DashboardDateRangeInfo MapDateRange(DashboardDateRange dateRange) =>
        new()
        {
            StartDate = dateRange.StartDate,
            EndDate = dateRange.EndDate,
            Period = dateRange.Period
        };
}

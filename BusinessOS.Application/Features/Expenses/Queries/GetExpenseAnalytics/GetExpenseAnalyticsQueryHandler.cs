using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Expenses.Queries.GetExpenseAnalytics;

public sealed class GetExpenseAnalyticsQueryHandler
    : IRequestHandler<GetExpenseAnalyticsQuery, ExpenseAnalyticsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetExpenseAnalyticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExpenseAnalyticsResponse> Handle(
        GetExpenseAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Expenses.AsNoTracking();

        if (request.DateFrom.HasValue)
        {
            var dateFrom = request.DateFrom.Value.ToUniversalTime();
            query = query.Where(x => x.ExpenseDate >= dateFrom);
        }

        if (request.DateTo.HasValue)
        {
            var dateTo = request.DateTo.Value.ToUniversalTime();
            query = query.Where(x => x.ExpenseDate <= dateTo);
        }

        var totalExpenses = await query.SumAsync(x => x.Amount, cancellationToken);
        var totalCount = await query.CountAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthlyExpenses = await query
            .Where(x => x.ExpenseDate >= monthStart)
            .SumAsync(x => x.Amount, cancellationToken);

        var topCategories = await query
            .GroupBy(x => x.ExpenseCategory.Name)
            .Select(g => new ExpenseCategoryBreakdown
            {
                CategoryName = g.Key,
                Amount = g.Sum(x => x.Amount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var category in topCategories)
        {
            category.Amount = Math.Round(category.Amount, 2);
            category.Percentage = totalExpenses > 0
                ? Math.Round(category.Amount / totalExpenses * 100, 2)
                : 0;
        }

        var trendRows = await query
            .GroupBy(x => new { x.ExpenseDate.Year, x.ExpenseDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Amount = g.Sum(x => x.Amount),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var trends = trendRows
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => new ExpenseTrendPoint
            {
                Period = $"{x.Year}-{x.Month:D2}",
                Amount = Math.Round(x.Amount, 2),
                Count = x.Count
            })
            .ToList();

        return new ExpenseAnalyticsResponse
        {
            TotalExpenses = Math.Round(totalExpenses, 2),
            MonthlyExpenses = Math.Round(monthlyExpenses, 2),
            TotalCount = totalCount,
            TopCategories = topCategories,
            Trends = trends
        };
    }
}

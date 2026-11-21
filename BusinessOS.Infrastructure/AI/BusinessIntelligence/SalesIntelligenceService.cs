using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.BusinessIntelligence;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.BusinessIntelligence;

public sealed class SalesIntelligenceService : ISalesIntelligenceService
{
    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IPredictionAccuracyService _predictions;

    public SalesIntelligenceService(
        BusinessOSDbContext db,
        ICurrentUserService user,
        IPredictionAccuracyService predictions)
    {
        _db = db;
        _user = user;
        _predictions = predictions;
    }

    public async Task<SalesIntelligenceDto> GetIntelligenceAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? period = null,
        CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.OrderView, PermissionCodes.AnalyticsView, PermissionCodes.NalvaView);
        var (start, end, label, currency) = await ResolvePeriodAsync(startDate, endDate, period, cancellationToken);
        var length = end - start;
        var priorStart = start - length;
        var priorEnd = start.AddTicks(-1);

        var revenue = await CompletedRevenueAsync(start, end, cancellationToken);
        var orderCount = await CompletedOrderCountAsync(start, end, cancellationToken);
        var aov = orderCount > 0 ? decimal.Round(revenue / orderCount, 2) : 0m;
        var forecast = await ForecastSalesAsync(1, cancellationToken);
        var top = await GetProductPerformanceAsync(cancellationToken);
        var declining = await GetDecliningProductsAsync(cancellationToken);
        var conversion = await GetConversionAsync(cancellationToken);
        var opportunities = await GetRevenueOpportunitiesAsync(cancellationToken);

        return new SalesIntelligenceDto(
            currency,
            label,
            start,
            end,
            revenue,
            aov,
            orderCount,
            forecast,
            top.Take(10).ToList(),
            declining,
            conversion,
            opportunities,
            [
                "Sales revenue = SUM(completed order GrandTotal) in the selected period.",
                "Product performance uses completed non-cancelled order lines only.",
                "Forecasts are estimates — not confirmed financial facts."
            ],
            BuildSalesLimitations(forecast, priorStart, priorEnd),
            "REAL");
    }

    public async Task<RevenueForecastDto> ForecastSalesAsync(int horizonMonths = 1, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.OrderView, PermissionCodes.AnalyticsView, PermissionCodes.NalvaView);
        horizonMonths = Math.Clamp(horizonMonths, 1, 6);
        var currency = await _db.Tenants.AsNoTracking().Select(x => x.Currency).FirstOrDefaultAsync(cancellationToken) ?? "USD";
        var months = await LoadMonthlyRevenueAsync(18, cancellationToken);
        var historyValues = months.Select(x => x.Value).ToList();
        var engine = ForecastingEngine.Forecast(historyValues, horizonMonths, "completed-order-revenue");

        var historyPoints = months.Select(x => new HistoricalPointDto(x.PeriodStart, x.Value)).ToList();
        var horizon = new List<ForecastHorizonPointDto>();
        if (engine.Available)
        {
            var anchor = months.Count > 0
                ? months[^1].PeriodStart.AddMonths(1)
                : new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            foreach (var point in engine.Points)
            {
                var start = anchor.AddMonths(point.StepAhead - 1);
                var end = start.AddMonths(1).AddTicks(-1);
                horizon.Add(new ForecastHorizonPointDto(start, end, point.Value, point.Confidence, true));
            }

            var next = engine.Points[0];
            await _predictions.RecordForecastAsync(new PredictionRecordRequest
            {
                MetricKey = "sales-revenue-monthly",
                Method = engine.Method,
                PredictedValue = next.Value,
                Confidence = next.Confidence,
                TargetPeriodStart = horizon[0].PeriodStart,
                TargetPeriodEnd = horizon[0].PeriodEnd,
                AssumptionsJson = string.Join("|", engine.Assumptions)
            }, cancellationToken);
        }

        return new RevenueForecastDto(
            currency,
            engine.Available,
            engine.Confidence,
            engine.Method,
            engine.Available ? engine.Points[0].Value : null,
            historyPoints,
            horizon,
            engine.MeanAbsolutePercentageError,
            engine.Assumptions,
            engine.Limitations,
            "REAL",
            true);
    }

    public async Task<IReadOnlyList<ProductPerformanceDto>> GetProductPerformanceAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.OrderView, PermissionCodes.ProductView, PermissionCodes.NalvaView);
        var now = DateTime.UtcNow;
        var currentStart = now.AddDays(-30);
        var priorStart = now.AddDays(-60);

        var current = await AggregateProductSalesAsync(currentStart, now, cancellationToken);
        var prior = await AggregateProductSalesAsync(priorStart, currentStart, cancellationToken);

        return current
            .Select(row =>
            {
                var priorRev = prior.TryGetValue(row.Key, out var priorRow) ? priorRow.Revenue : 0m;
                decimal? change = priorRev > 0
                    ? decimal.Round((row.Value.Revenue - priorRev) / priorRev * 100m, 2)
                    : row.Value.Revenue > 0 ? 100m : 0m;
                var trend = change switch
                {
                    > 5m => "rising",
                    < -5m => "declining",
                    _ => "stable"
                };
                return new ProductPerformanceDto(
                    row.Key,
                    row.Value.Name,
                    row.Value.Sku,
                    row.Value.Units,
                    row.Value.Revenue,
                    priorRev > 0 ? priorRev : null,
                    change,
                    trend,
                    priorRev > 0 ? "medium" : "low");
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }

    public async Task<IReadOnlyList<DecliningProductDto>> GetDecliningProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await GetProductPerformanceAsync(cancellationToken);
        return products
            .Where(x => x.Trend == "declining" && x.PriorPeriodRevenue is > 0)
            .Select(x => new DecliningProductDto(
                x.ProductId,
                x.ProductName,
                x.Sku,
                x.Revenue,
                x.PriorPeriodRevenue ?? 0m,
                Math.Abs(x.ChangePercent ?? 0m),
                $"Revenue fell {Math.Abs(x.ChangePercent ?? 0m):0.##}% vs the prior 30 days (completed orders only)."))
            .OrderByDescending(x => x.DeclinePercent)
            .Take(20)
            .ToList();
    }

    public async Task<ConversionAnalyticsDto> GetConversionAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.OrderView, PermissionCodes.AnalyticsView, PermissionCodes.NalvaView);
        var totalLeads = await _db.Leads.AsNoTracking().CountAsync(cancellationToken);
        var converted = await _db.Leads.AsNoTracking().CountAsync(x => x.Status == LeadStatuses.Converted, cancellationToken);
        var completed = await _db.Orders.AsNoTracking().CountAsync(x => x.Status == OrderStatusNames.Completed, cancellationToken);
        var cancelled = await _db.Orders.AsNoTracking().CountAsync(x => x.Status == OrderStatusNames.Cancelled, cancellationToken);
        var orderDenom = completed + cancelled;

        var limitations = new List<string>();
        if (totalLeads == 0)
            limitations.Add("No leads recorded; lead conversion rate is zero by definition, not a forecast.");

        return new ConversionAnalyticsDto(
            totalLeads,
            converted,
            totalLeads == 0 ? 0m : decimal.Round((decimal)converted / totalLeads, 4),
            completed,
            cancelled,
            orderDenom == 0 ? 0m : decimal.Round((decimal)completed / orderDenom, 4),
            "REAL",
            limitations);
    }

    public async Task<IReadOnlyList<BiRecommendationDto>> GetRevenueOpportunitiesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<BiRecommendationDto>();
        var declining = await GetDecliningProductsAsync(cancellationToken);
        var top = (await GetProductPerformanceAsync(cancellationToken)).Where(x => x.Trend == "rising").Take(3).ToList();
        var conversion = await GetConversionAsync(cancellationToken);

        foreach (var product in top)
        {
            list.Add(new BiRecommendationDto(
                $"promote-{product.ProductId:N}",
                "sales",
                $"Promote {product.ProductName}",
                "Rising completed-order revenue over the last 30 days relative to the prior window.",
                [$"Revenue 30d: {product.Revenue}", $"Change: {product.ChangePercent}%"],
                "marketing",
                $"Promote high-performing product {product.ProductName} (SKU {product.Sku}).",
                RequiresHumanApproval: true,
                IsPrediction: false,
                Confidence: product.Confidence));
        }

        foreach (var product in declining.Take(3))
        {
            list.Add(new BiRecommendationDto(
                $"review-decline-{product.ProductId:N}",
                "sales",
                $"Review declining product {product.ProductName}",
                product.Explanation,
                [$"Current: {product.CurrentRevenue}", $"Prior: {product.PriorRevenue}", $"Decline: {product.DeclinePercent}%"],
                "sales",
                $"Investigate declining sales for {product.ProductName} and propose a recovery plan.",
                RequiresHumanApproval: false,
                IsPrediction: false,
                Confidence: "medium"));
        }

        if (conversion.TotalLeads >= 5 && conversion.LeadConversionRate < 0.1m)
        {
            list.Add(new BiRecommendationDto(
                "improve-lead-conversion",
                "sales",
                "Improve lead conversion",
                "Lead conversion from stored CRM records is below 10%.",
                [$"Leads: {conversion.TotalLeads}", $"Converted: {conversion.ConvertedLeads}", $"Rate: {conversion.LeadConversionRate}"],
                "sales",
                "Review open leads and draft follow-ups to improve conversion.",
                RequiresHumanApproval: true,
                IsPrediction: false,
                Confidence: "medium"));
        }

        return list;
    }

    private async Task<(DateTime Start, DateTime End, string Label, string Currency)> ResolvePeriodAsync(
        DateTime? startDate, DateTime? endDate, string? period, CancellationToken ct)
    {
        var currency = await _db.Tenants.AsNoTracking().Select(x => x.Currency).FirstOrDefaultAsync(ct) ?? "USD";
        var now = DateTime.UtcNow;
        if (startDate is not null && endDate is not null)
            return (startDate.Value.ToUniversalTime(), endDate.Value.ToUniversalTime(), "custom", currency);

        var p = (period ?? "month").Trim().ToLowerInvariant();
        return p switch
        {
            "week" => (now.Date.AddDays(-7), now, "week", currency),
            "year" => (new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), now, "year", currency),
            "quarter" => (now.Date.AddMonths(-3), now, "quarter", currency),
            _ => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), now, "month", currency)
        };
    }

    private async Task<decimal> CompletedRevenueAsync(DateTime start, DateTime end, CancellationToken ct) =>
        await _db.Orders.AsNoTracking()
            .Where(x => x.Status == OrderStatusNames.Completed && x.OrderDate >= start && x.OrderDate <= end)
            .SumAsync(x => (decimal?)x.GrandTotal, ct) ?? 0m;

    private async Task<int> CompletedOrderCountAsync(DateTime start, DateTime end, CancellationToken ct) =>
        await _db.Orders.AsNoTracking()
            .CountAsync(x => x.Status == OrderStatusNames.Completed && x.OrderDate >= start && x.OrderDate <= end, ct);

    private async Task<List<(DateTime PeriodStart, decimal Value)>> LoadMonthlyRevenueAsync(int months, CancellationToken ct)
    {
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));
        var rows = await _db.Orders.AsNoTracking()
            .Where(x => x.Status == OrderStatusNames.Completed && x.OrderDate >= start)
            .Select(x => new { x.OrderDate, x.GrandTotal })
            .ToListAsync(ct);

        var result = new List<(DateTime, decimal)>();
        for (var i = 0; i < months; i++)
        {
            var periodStart = start.AddMonths(i);
            var periodEnd = periodStart.AddMonths(1);
            var sum = rows.Where(x => x.OrderDate >= periodStart && x.OrderDate < periodEnd).Sum(x => x.GrandTotal);
            result.Add((periodStart, decimal.Round(sum, 2)));
        }

        // Drop leading all-zero months so forecasts aren't skewed by empty onboarding months,
        // but keep at least the trailing contiguous series including zeros after first sale.
        var firstSale = result.FindIndex(x => x.Item2 > 0);
        if (firstSale > 0)
            result = result.Skip(firstSale).ToList();
        return result;
    }

    private async Task<Dictionary<Guid, (string Name, string Sku, decimal Units, decimal Revenue)>> AggregateProductSalesAsync(
        DateTime start, DateTime end, CancellationToken ct)
    {
        var rows = await _db.OrderItems.AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.Order.Status == OrderStatusNames.Completed
                && x.Order.OrderDate >= start
                && x.Order.OrderDate < end)
            .GroupBy(x => new { x.ProductId, Name = x.Product!.Name, Sku = x.Product.SKU })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Name,
                g.Key.Sku,
                Units = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Total)
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            x => x.ProductId,
            x => (Name: x.Name, Sku: x.Sku, Units: x.Units, Revenue: decimal.Round(x.Revenue, 2)));
    }

    private static List<string> BuildSalesLimitations(RevenueForecastDto forecast, DateTime priorStart, DateTime priorEnd)
    {
        var list = new List<string>
        {
            "Pending and cancelled orders are excluded from revenue and product rankings.",
            $"Prior comparison window used for product trends: {priorStart:yyyy-MM-dd} to {priorEnd:yyyy-MM-dd}."
        };
        list.AddRange(forecast.Limitations);
        return list;
    }

    private void RequireAny(params string[] permissions)
    {
        if (permissions.Any(_user.HasPermission))
            return;
        throw new ForbiddenException($"Missing permission {string.Join(" or ", permissions)}.");
    }
}

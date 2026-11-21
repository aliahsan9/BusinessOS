using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.BusinessIntelligence;
using BusinessOS.Application.Features.FinanceAgent;
using BusinessOS.Application.Features.ProcurementAgent;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.BusinessIntelligence;

public sealed class BusinessIntelligenceService : IBusinessIntelligenceService
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _user;
    private readonly ICacheService _cache;
    private readonly ISalesIntelligenceService _sales;
    private readonly ICustomerIntelligenceService _customers;
    private readonly IFinanceAgentService _finance;
    private readonly IInventoryProcurementAgentService _procurement;
    private readonly IPredictionAccuracyService _predictions;

    public BusinessIntelligenceService(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ICurrentUserService user,
        ICacheService cache,
        ISalesIntelligenceService sales,
        ICustomerIntelligenceService customers,
        IFinanceAgentService finance,
        IInventoryProcurementAgentService procurement,
        IPredictionAccuracyService predictions)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _cache = cache;
        _sales = sales;
        _customers = customers;
        _finance = finance;
        _procurement = procurement;
        _predictions = predictions;
    }

    public async Task<BusinessHealthOverviewDto> GetHealthOverviewAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? period = null,
        CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.AnalyticsView, PermissionCodes.NalvaView, PermissionCodes.ReportView);
        var finance = await _finance.GetIntelligenceAsync(startDate, endDate, period, cancellationToken);
        var risks = await _procurement.GetInventoryRisksAsync(cancellationToken);
        var churn = await _customers.GetChurnRisksAsync(cancellationToken);
        var overdue = await _db.Invoices.AsNoTracking()
            .CountAsync(x => x.OutstandingAmount > 0
                && x.DueDate < DateTime.UtcNow
                && x.Status != "Paid"
                && x.Status != "Cancelled"
                && x.Status != "Draft", cancellationToken);

        var health = ScoreHealth(
            finance.SalesRevenue.Value,
            finance.GrossProfit.Available ? finance.GrossProfit.Value : null,
            risks.Count(x => x.RiskKind is "out-of-stock" or "low-stock"),
            churn.Count(x => x.RiskLevel == "high"),
            overdue);

        return new BusinessHealthOverviewDto(
            finance.Currency,
            finance.Period,
            finance.StartDate,
            finance.EndDate,
            new BiMetricDto(finance.SalesRevenue.Value, finance.SalesRevenue.Available, false, "high", "completed orders"),
            new BiMetricDto(finance.CollectedCash.Value, finance.CollectedCash.Available, false, "high", "completed payments net of refunds"),
            new BiMetricDto(finance.TotalExpenses.Value, finance.TotalExpenses.Available, false, "high", "approved/paid expenses"),
            new BiMetricDto(finance.GrossProfit.Value, finance.GrossProfit.Available, false, finance.GrossProfit.Available ? "medium" : "unavailable", "sales − COGS when cost present"),
            new BiMetricDto(finance.OutstandingReceivables.Value, finance.OutstandingReceivables.Available, false, "high", "invoice outstanding"),
            new BiMetricDto(finance.OutstandingPayables.Value, finance.OutstandingPayables.Available, false, "high", "purchase invoice outstanding"),
            risks.Count(x => x.RiskKind is "out-of-stock" or "low-stock"),
            churn.Count(x => x.RiskLevel is "high" or "medium"),
            overdue,
            health,
            finance.Formulas,
            finance.Limitations.Append("Overall health label is a heuristic from recorded metrics, not a credit rating.").ToList(),
            "REAL");
    }

    public Task<RevenueForecastDto> ForecastRevenueAsync(int horizonMonths = 1, CancellationToken cancellationToken = default) =>
        _sales.ForecastSalesAsync(horizonMonths, cancellationToken);

    public async Task<PeriodComparisonDto> ComparePeriodsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? period = null,
        CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.AnalyticsView, PermissionCodes.NalvaView, PermissionCodes.ReportView);
        var finance = await _finance.GetIntelligenceAsync(startDate, endDate, period, cancellationToken);
        var length = finance.EndDate - finance.StartDate;
        var priorStart = finance.StartDate - length;
        var priorEnd = finance.StartDate.AddTicks(-1);

        var currentRevenue = finance.SalesRevenue.Value ?? 0m;
        var priorRevenue = await _db.Orders.AsNoTracking()
            .Where(x => x.Status == OrderStatusNames.Completed && x.OrderDate >= priorStart && x.OrderDate <= priorEnd)
            .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0m;
        var currentOrders = await _db.Orders.AsNoTracking()
            .CountAsync(x => x.Status == OrderStatusNames.Completed && x.OrderDate >= finance.StartDate && x.OrderDate <= finance.EndDate, cancellationToken);
        var priorOrders = await _db.Orders.AsNoTracking()
            .CountAsync(x => x.Status == OrderStatusNames.Completed && x.OrderDate >= priorStart && x.OrderDate <= priorEnd, cancellationToken);
        var currentExpenses = finance.TotalExpenses.Value ?? 0m;
        var priorExpenses = await _db.Expenses.AsNoTracking()
            .Where(x => (x.Status == ExpenseStatusNames.Approved || x.Status == ExpenseStatusNames.Paid)
                && x.ExpenseDate >= priorStart && x.ExpenseDate <= priorEnd)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        decimal? revChange = priorRevenue > 0
            ? decimal.Round((currentRevenue - priorRevenue) / priorRevenue * 100m, 2)
            : currentRevenue > 0 ? 100m : 0m;
        decimal? expChange = priorExpenses > 0
            ? decimal.Round((currentExpenses - priorExpenses) / priorExpenses * 100m, 2)
            : currentExpenses > 0 ? 100m : 0m;

        return new PeriodComparisonDto(
            finance.Currency,
            finance.StartDate,
            finance.EndDate,
            priorStart,
            priorEnd,
            currentRevenue,
            priorRevenue,
            revChange,
            currentOrders,
            priorOrders,
            currentExpenses,
            priorExpenses,
            expChange,
            "REAL");
    }

    public async Task<IReadOnlyList<BusinessRiskDto>> GetRisksAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.AnalyticsView, PermissionCodes.NalvaView, PermissionCodes.ReportView);
        var list = new List<BusinessRiskDto>();
        var inventory = await _procurement.GetInventoryRisksAsync(cancellationToken);
        foreach (var risk in inventory.Where(x => x.RiskKind is "out-of-stock" or "low-stock").Take(10))
        {
            list.Add(new BusinessRiskDto(
                "inventory",
                risk.RiskKind == "out-of-stock" ? "high" : "medium",
                $"{risk.ProductName}: {risk.RiskKind}",
                risk.Explanation,
                [$"On hand: {risk.CurrentStock}", $"Inbound: {risk.IncomingQuantity}", $"Suggested reorder: {risk.SuggestedReorderQuantity}"],
                "procurement",
                IsPrediction: risk.EstimatedStockoutDate is not null));
        }

        var cash = await _finance.GetCashFlowAsync(projectionDays: 30, cancellationToken: cancellationToken);
        foreach (var warning in cash.ShortageWarnings.Take(5))
        {
            list.Add(new BusinessRiskDto(
                "finance",
                "medium",
                "Cash-flow warning",
                warning,
                [$"Projection available: {cash.ProjectionAvailable}", $"Confidence: {cash.ProjectionConfidence}"],
                "finance",
                IsPrediction: true));
        }

        var churn = (await _customers.GetChurnRisksAsync(cancellationToken)).Where(x => x.RiskLevel == "high").Take(5);
        foreach (var c in churn)
        {
            list.Add(new BusinessRiskDto(
                "customer",
                "medium",
                $"Potential lost customer: {c.CustomerName}",
                c.Explanation,
                [$"Days since order: {c.DaysSinceLastOrder}", $"Lifetime revenue: {c.LifetimeRevenue}"],
                "support",
                IsPrediction: true));
        }

        var declining = await _sales.GetDecliningProductsAsync(cancellationToken);
        foreach (var d in declining.Take(5))
        {
            list.Add(new BusinessRiskDto(
                "sales",
                "medium",
                $"Declining product: {d.ProductName}",
                d.Explanation,
                [$"Decline: {d.DeclinePercent}%"],
                "sales",
                IsPrediction: false));
        }

        return list
            .OrderBy(x => x.Severity == "high" ? 0 : x.Severity == "medium" ? 1 : 2)
            .ToList();
    }

    public async Task<IReadOnlyList<BiRecommendationDto>> GetRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.AnalyticsView, PermissionCodes.NalvaView, PermissionCodes.ReportView);
        var sales = await _sales.GetRevenueOpportunitiesAsync(cancellationToken);
        var customers = await _customers.GetRetentionStrategiesAsync(cancellationToken);
        var procurement = await _procurement.GetRecommendationsAsync(cancellationToken);
        var list = new List<BiRecommendationDto>();
        list.AddRange(sales);
        list.AddRange(customers);

        foreach (var rec in procurement.Take(5))
        {
            list.Add(new BiRecommendationDto(
                $"reorder-{rec.ProductId:N}",
                "inventory",
                $"Reorder {rec.ProductName}",
                rec.Reason,
                [$"Suggested qty: {rec.SuggestedQuantity}", $"Confidence: {rec.Confidence}"],
                "procurement",
                $"Prepare purchase recommendation for {rec.ProductName} qty {rec.SuggestedQuantity}.",
                RequiresHumanApproval: true,
                IsPrediction: true,
                Confidence: rec.Confidence));
        }

        return list;
    }

    public async Task<ExecutiveDashboardDto> GetExecutiveDashboardAsync(string? period = null, CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.AnalyticsView, PermissionCodes.NalvaView, PermissionCodes.ReportView);

        if (_tenant.HasTenant())
        {
            var cacheKey = CacheKeys.BusinessIntelligence(_tenant.TenantId, $"exec:{period ?? "month"}");
            return await _cache.GetOrSetAsync(cacheKey, async ct => await BuildDashboardAsync(period, ct),
                TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
        }

        return await BuildDashboardAsync(period, cancellationToken);
    }

    private async Task<ExecutiveDashboardDto> BuildDashboardAsync(string? period, CancellationToken ct)
    {
        var health = await GetHealthOverviewAsync(period: period, cancellationToken: ct);
        var forecast = await ForecastRevenueAsync(1, ct);
        var comparison = await ComparePeriodsAsync(period: period, cancellationToken: ct);
        var sales = await _sales.GetIntelligenceAsync(period: period, cancellationToken: ct);
        var customers = await _customers.GetIntelligenceAsync(ct);
        var inventoryRisks = await _procurement.GetInventoryRisksAsync(ct);
        var demand = await _procurement.AnalyzeDemandAsync(cancellationToken: ct);
        var turnover = await BuildInventoryTurnoverAsync(ct);
        CashFlowIntelligenceDto? cash = null;
        FinanceIntelligenceDto? finance = null;
        try
        {
            cash = await _finance.GetCashFlowAsync(projectionDays: 30, cancellationToken: ct);
            finance = await _finance.GetIntelligenceAsync(period: period, cancellationToken: ct);
        }
        catch (ForbiddenException)
        {
            // Finance permissions may be narrower than analytics; dashboard still returns other panels.
        }

        var risks = await GetRisksAsync(ct);
        var recommendations = await GetRecommendationsAsync(ct);
        var accuracy = await _predictions.GetAccuracySummaryAsync(ct);

        var limitations = new List<string>
        {
            "Actual figures come from verified tenant database records.",
            "Forecast panels are labeled predictions and are not confirmed financial facts.",
            "Inventory lead times are derived from historical purchase receive lag when available."
        };
        if (cash is not null)
            limitations.AddRange(cash.Limitations);
        limitations.AddRange(forecast.Limitations);

        return new ExecutiveDashboardDto(
            health,
            forecast,
            comparison,
            sales,
            customers,
            inventoryRisks.Take(20).ToList(),
            demand.Take(20).ToList(),
            turnover,
            cash,
            finance,
            risks,
            recommendations,
            accuracy,
            "REAL",
            limitations.Distinct().ToList());
    }

    public async Task<IReadOnlyList<InventoryTurnoverDto>> BuildInventoryTurnoverAsync(CancellationToken cancellationToken = default)
    {
        var inventories = await _db.Inventories.AsNoTracking()
            .Where(x => x.Product.IsActive)
            .Select(x => new
            {
                x.ProductId,
                Name = x.Product.Name,
                Sku = x.Product.SKU,
                x.CurrentStock
            })
            .ToListAsync(cancellationToken);

        var since = DateTime.UtcNow.AddDays(-90);
        var sales = await _db.OrderItems.AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.Order.Status == OrderStatusNames.Completed
                && x.Order.OrderDate >= since)
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Units, cancellationToken);

        var leadTimes = await LoadLeadTimesAsync(cancellationToken);
        var list = new List<InventoryTurnoverDto>();
        foreach (var row in inventories)
        {
            sales.TryGetValue(row.ProductId, out var units);
            var avgStock = row.CurrentStock; // single snapshot proxy; documented limitation
            decimal? turnover = avgStock > 0 ? decimal.Round(units / avgStock, 2) : null;
            decimal? days = units > 0 ? decimal.Round(avgStock / (units / 90m), 1) : null;
            leadTimes.TryGetValue(row.ProductId, out var lead);
            var confidence = units > 0 ? (avgStock > 0 ? "medium" : "low") : "unavailable";
            list.Add(new InventoryTurnoverDto(
                row.ProductId,
                row.Name,
                row.Sku,
                units,
                avgStock,
                turnover,
                days,
                lead > 0 ? lead : null,
                confidence,
                avgStock <= 0 && units == 0
                    ? "No stock and no sales in 90 days."
                    : $"Turnover uses 90-day completed sales ÷ current on-hand as a snapshot proxy. Lead time {(lead > 0 ? lead + " day(s) from PO→receive history" : "unavailable")}."));
        }

        return list
            .OrderByDescending(x => x.DaysOfInventory ?? 0)
            .Take(50)
            .ToList();
    }

    private async Task<Dictionary<Guid, int>> LoadLeadTimesAsync(CancellationToken ct)
    {
        var received = await _db.PurchaseReceivingEvents.AsNoTracking()
            .Where(x => x.CreatedAt != default)
            .Select(x => new { x.PurchaseId, x.CreatedAt })
            .ToListAsync(ct);
        if (received.Count == 0)
            return new Dictionary<Guid, int>();

        var poIds = received.Select(x => x.PurchaseId).Distinct().ToList();
        var purchases = await _db.Purchases.AsNoTracking()
            .Where(x => poIds.Contains(x.Id))
            .Select(x => new { x.Id, x.PurchaseDate })
            .ToDictionaryAsync(x => x.Id, ct);

        var items = await _db.PurchaseItems.AsNoTracking()
            .Where(x => poIds.Contains(x.PurchaseId) && !x.IsDeleted)
            .Select(x => new { x.PurchaseId, x.ProductId })
            .ToListAsync(ct);

        var byProduct = new Dictionary<Guid, List<int>>();
        foreach (var evt in received)
        {
            if (!purchases.TryGetValue(evt.PurchaseId, out var po))
                continue;
            var days = (int)Math.Max(0, (evt.CreatedAt.Date - po.PurchaseDate.Date).TotalDays);
            foreach (var item in items.Where(x => x.PurchaseId == evt.PurchaseId))
            {
                if (!byProduct.TryGetValue(item.ProductId, out var list))
                {
                    list = [];
                    byProduct[item.ProductId] = list;
                }
                list.Add(days);
            }
        }

        return byProduct.ToDictionary(
            x => x.Key,
            x => (int)Math.Round(x.Value.Average()));
    }

    private static string ScoreHealth(
        decimal? revenue,
        decimal? gross,
        int stockRisks,
        int highChurn,
        int overdue)
    {
        var score = 100;
        if (revenue is null or 0) score -= 15;
        if (gross is < 0) score -= 20;
        score -= Math.Min(25, stockRisks * 5);
        score -= Math.Min(20, highChurn * 4);
        score -= Math.Min(20, overdue * 2);
        return score switch
        {
            >= 80 => "healthy",
            >= 60 => "watch",
            >= 40 => "at-risk",
            _ => "critical"
        };
    }

    private void RequireAny(params string[] permissions)
    {
        if (permissions.Any(_user.HasPermission))
            return;
        throw new ForbiddenException($"Missing permission {string.Join(" or ", permissions)}.");
    }
}

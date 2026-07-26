using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Copilot.Tools;

public sealed class GetRevenueTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetRevenueTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetRevenue;
    public override string Description => "Calculate revenue totals by period (month, quarter, year).";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.Analytics or AiCopilotIntent.FollowUp
        && ContainsAny(message, "revenue", "income", "earnings", "profit");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var (start, end, label) = ResolveDateRange(context.Message);

        var revenue = await _context.Invoices
            .Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end)
            .SumAsync(i => i.GrandTotal, cancellationToken);

        var paid = await _context.Invoices
            .Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end)
            .SumAsync(i => i.AmountPaid, cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { revenue, paid, period = label, start, end },
            Summary = $"Total revenue {label}: {revenue:C} ({paid:C} collected)."
        };
    }
}

public sealed class GetSalesSummaryTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetSalesSummaryTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetSalesSummary;
    public override string Description => "Summarize products sold, order counts, and sales volume.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.Analytics or AiCopilotIntent.BusinessIntelligence or AiCopilotIntent.FollowUp
        && ContainsAny(message, "sold", "sales", "products", "orders", "how many");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var (start, end, label) = ResolveDateRange(context.Message);

        var productsSold = await _context.OrderItems
            .Where(oi => !oi.IsDeleted && oi.Order.OrderDate >= start && oi.Order.OrderDate <= end)
            .SumAsync(oi => oi.Quantity, cancellationToken);

        var orderCount = await _context.Orders
            .Where(o => o.OrderDate >= start && o.OrderDate <= end)
            .CountAsync(cancellationToken);

        var orderTotal = await _context.Orders
            .Where(o => o.OrderDate >= start && o.OrderDate <= end)
            .SumAsync(o => o.GrandTotal, cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { productsSold, orderCount, orderTotal, period = label },
            Summary = $"{productsSold:N0} products sold across {orderCount:N0} orders {label} ({orderTotal:C} total)."
        };
    }
}

public sealed class GetBestSellingProductsTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetBestSellingProductsTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetBestSellingProducts;
    public override string Description => "Rank best-selling products by units sold and revenue.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.Analytics or AiCopilotIntent.BusinessIntelligence or AiCopilotIntent.FollowUp or AiCopilotIntent.ActionRead
        && ContainsAny(message,
            "best selling", "best-selling", "bestseller", "best seller", "top product", "top selling",
            "most sold", "highest selling", "which product", "popular product", "product performance");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var (start, end, label) = ResolveDateRange(context.Message);

        var ranked = await _context.OrderItems
            .Where(oi => !oi.IsDeleted && oi.Order.OrderDate >= start && oi.Order.OrderDate <= end)
            .GroupBy(oi => new { oi.ProductId, Name = oi.Product != null ? oi.Product.Name : "Unknown product", Sku = oi.Product != null ? oi.Product.SKU : "" })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Name,
                g.Key.Sku,
                UnitsSold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Total)
            })
            .OrderByDescending(x => x.UnitsSold)
            .ThenByDescending(x => x.Revenue)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (ranked.Count == 0)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = new { period = label, products = Array.Empty<object>() },
                Summary = $"No product sales found {label}. I can't rank bestsellers without order line items in that period."
            };
        }

        var lines = ranked.Select((p, i) =>
            $"{i + 1}. {p.Name}{(string.IsNullOrWhiteSpace(p.Sku) ? "" : $" ({p.Sku})")}: {p.UnitsSold:N0} units, {p.Revenue:C} revenue");

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { period = label, products = ranked },
            Summary = $"Best-selling products {label}:\n" + string.Join("\n", lines)
        };
    }
}

public sealed class GetSalesTrendsTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetSalesTrendsTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetSalesTrends;
    public override string Description => "Compare sales and revenue across recent periods to surface trends.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.Analytics or AiCopilotIntent.BusinessIntelligence or AiCopilotIntent.FollowUp
        && ContainsAny(message, "trend", "growth", "growing", "declin", "compare", "vs last", "versus", "forecast", "future", "momentum");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var priorMonthStart = thisMonthStart.AddMonths(-2);

        async Task<(decimal Revenue, int Orders, decimal Units)> SnapshotAsync(DateTime start, DateTime end)
        {
            var revenue = await _context.Orders
                .Where(o => o.OrderDate >= start && o.OrderDate <= end)
                .SumAsync(o => (decimal?)o.GrandTotal, cancellationToken) ?? 0m;
            var orders = await _context.Orders
                .Where(o => o.OrderDate >= start && o.OrderDate <= end)
                .CountAsync(cancellationToken);
            var units = await _context.OrderItems
                .Where(oi => !oi.IsDeleted && oi.Order.OrderDate >= start && oi.Order.OrderDate <= end)
                .SumAsync(oi => (decimal?)oi.Quantity, cancellationToken) ?? 0m;
            return (revenue, orders, units);
        }

        var current = await SnapshotAsync(thisMonthStart, now);
        var previous = await SnapshotAsync(lastMonthStart, thisMonthStart.AddTicks(-1));
        var prior = await SnapshotAsync(priorMonthStart, lastMonthStart.AddTicks(-1));

        static string PctChange(decimal currentValue, decimal previousValue)
        {
            if (previousValue == 0)
                return currentValue == 0 ? "flat (no prior baseline)" : "new activity vs zero prior period";
            var pct = (currentValue - previousValue) / previousValue * 100m;
            var direction = pct >= 0 ? "up" : "down";
            return $"{direction} {Math.Abs(pct):N1}% vs prior period";
        }

        var revenueTrend = PctChange(current.Revenue, previous.Revenue);
        var orderTrend = PctChange(current.Orders, previous.Orders);
        var unitTrend = PctChange(current.Units, previous.Units);

        var momentum = current.Revenue >= previous.Revenue && previous.Revenue >= prior.Revenue
            ? "Revenue is rising across the last three months — momentum is positive."
            : current.Revenue < previous.Revenue && previous.Revenue < prior.Revenue
                ? "Revenue has declined for two consecutive months — prioritize retention and conversion."
                : "Revenue is mixed month-to-month — focus on consistent bestsellers and follow-ups.";

        var summary =
            $"""
            Sales trends (based on live orders):
            • This month so far: {current.Revenue:C} revenue, {current.Orders:N0} orders, {current.Units:N0} units ({revenueTrend}).
            • Last month: {previous.Revenue:C} revenue, {previous.Orders:N0} orders, {previous.Units:N0} units.
            • Month before last: {prior.Revenue:C} revenue, {prior.Orders:N0} orders, {prior.Units:N0} units.
            • Orders trend: {orderTrend}. Units trend: {unitTrend}.
            • Outlook from your data: {momentum}
            Note: Future projections are directional from recent history, not guarantees.
            """;

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new
            {
                thisMonth = current,
                lastMonth = previous,
                monthBeforeLast = prior,
                revenueTrend,
                orderTrend,
                unitTrend,
                momentum
            },
            Summary = summary.Trim()
        };
    }
}

using System.Text;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Copilot.Tools;

/// <summary>
/// Calendar boundaries in the tenant timezone. UTC is used when the business has no configured zone.
/// </summary>
internal static class BusinessClock
{
    public static async Task<TimeZoneInfo> LoadAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var id = await context.Tenants.AsNoTracking().Select(t => t.Timezone).FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
            return zone;

        return TimeZoneInfo.Utc;
    }

    public static (DateTime Start, DateTime End, string Label) Resolve(string? message, TimeZoneInfo zone)
    {
        var text = ConversationLanguage.ExpandForRouting(message);
        var utcNow = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
        var zoneLabel = zone.Equals(TimeZoneInfo.Utc) ? "" : $" ({zone.Id})";

        if (Contains(text, "today"))
            return (ToUtc(localNow.Date, zone), utcNow, "today" + zoneLabel);

        if (Contains(text, "yesterday"))
        {
            var start = localNow.Date.AddDays(-1);
            return (ToUtc(start, zone), ToUtc(localNow.Date, zone).AddTicks(-1), "yesterday" + zoneLabel);
        }

        if (Contains(text, "last week", "previous week"))
        {
            var weekStart = StartOfWeek(localNow.Date);
            return (ToUtc(weekStart.AddDays(-7), zone), ToUtc(weekStart, zone).AddTicks(-1), "last week" + zoneLabel);
        }

        if (Contains(text, "this week"))
            return (ToUtc(StartOfWeek(localNow.Date), zone), utcNow, "this week" + zoneLabel);

        if (Contains(text, "last month", "previous month"))
        {
            var start = new DateTime(localNow.Year, localNow.Month, 1).AddMonths(-1);
            return (ToUtc(start, zone), ToUtc(start.AddMonths(1), zone).AddTicks(-1), "last month" + zoneLabel);
        }

        if (Contains(text, "this year", "last year", "previous year") && Contains(text, "last year", "previous year"))
        {
            var start = new DateTime(localNow.Year - 1, 1, 1);
            return (ToUtc(start, zone), ToUtc(start.AddYears(1), zone).AddTicks(-1), "last year" + zoneLabel);
        }

        if (Contains(text, "this year"))
            return (ToUtc(new DateTime(localNow.Year, 1, 1), zone), utcNow, "this year" + zoneLabel);

        var monthStart = new DateTime(localNow.Year, localNow.Month, 1);
        return (ToUtc(monthStart, zone), utcNow, "this month" + zoneLabel);
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);

    private static DateTime StartOfWeek(DateTime day)
    {
        var delta = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return day.AddDays(-delta);
    }

    private static bool Contains(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}

public sealed class GetInventoryHealthTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetInventoryHealthTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetInventoryHealth;
    public override string Description =>
        "Inventory health: on-hand stock, reorder levels, stock value at cost, fast and slow products, and reorder estimates from recent completed sales. Estimates are historical, not forecasts.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.BusinessIntelligence or AiCopilotIntent.Recommendation or AiCopilotIntent.ActionRead
        && ContainsAny(message, "inventory health", "inventory value", "stock value", "days of inventory", "reorder this week");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var rows = await LoadInventoryAsync(_context, cancellationToken);
        var sold = await LoadRecentSalesAsync(_context, cancellationToken);
        var summary = BuildInventorySummary(rows, sold);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { skuCount = rows.Count },
            Summary = summary
        };
    }

    internal static async Task<List<InventoryRow>> LoadInventoryAsync(IApplicationDbContext context, CancellationToken cancellationToken) =>
        await context.Inventories.AsNoTracking()
            .Select(i => new InventoryRow(
                i.ProductId,
                i.Product.Name,
                i.Product.SKU,
                i.CurrentStock,
                i.ReorderLevel,
                i.Product.CostPrice,
                i.Product.IsActive))
            .ToListAsync(cancellationToken);

    internal static async Task<Dictionary<Guid, decimal>> LoadRecentSalesAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var rows = await context.OrderItems.AsNoTracking()
            .Where(oi => !oi.IsDeleted
                && oi.Order.Status != OrderStatusNames.Cancelled
                && oi.Order.OrderDate >= since)
            .GroupBy(oi => oi.ProductId)
            .Select(g => new { g.Key, Units = g.Sum(x => x.Quantity) })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.Key, x => x.Units);
    }

    internal static string BuildInventorySummary(IReadOnlyList<InventoryRow> rows, IReadOnlyDictionary<Guid, decimal> unitsLast30Days)
    {
        if (rows.Count == 0)
            return "I checked inventory and found no stock records for this business. I can't value stock or recommend a reorder until products have inventory rows.";

        var active = rows.Where(r => r.IsActive).ToList();
        var stockValue = active.Sum(r => r.CurrentStock * r.CostPrice);
        var missingCost = active.Count(r => r.CurrentStock > 0 && r.CostPrice <= 0);
        var low = active.Where(r => r.CurrentStock > 0 && r.ReorderLevel > 0 && r.CurrentStock <= r.ReorderLevel).OrderBy(r => r.CurrentStock).Take(12).ToList();
        var outOfStock = active.Where(r => r.CurrentStock <= 0).Take(12).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Inventory covers {active.Count:N0} active product(s). Estimated stock value at cost is {stockValue:C}.");
        if (missingCost > 0)
            sb.AppendLine($"{missingCost:N0} stocked product(s) have no cost price, so that portion of the value is understated.");

        sb.AppendLine();
        sb.AppendLine("Product | Available | Reorder level | Recommendation");
        foreach (var row in low)
        {
            var daily = unitsLast30Days.TryGetValue(row.ProductId, out var units) ? units / 30m : 0m;
            var days = daily > 0 ? row.CurrentStock / daily : (decimal?)null;
            var daysText = days is null ? "no recent sales rate" : $"{days:N0} days at the last-30-day rate";
            sb.AppendLine($"{row.Name} ({row.Sku}) | {row.CurrentStock:N0} | {row.ReorderLevel:N0} | Replenish — {daysText}. This is an estimate from past sales, not a forecast.");
        }

        if (low.Count == 0)
            sb.AppendLine("No active product is above zero and at or below its reorder level. That is not the same as saying every product is healthy.");

        if (outOfStock.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Out of stock (available quantity is zero):");
            foreach (var row in outOfStock)
                sb.AppendLine($"- {row.Name} ({row.Sku}) | 0 | reorder {row.ReorderLevel:N0}");
        }

        var slow = active
            .Where(r => r.CurrentStock > 0 && !unitsLast30Days.ContainsKey(r.ProductId))
            .OrderByDescending(r => r.CurrentStock * r.CostPrice)
            .Take(8)
            .ToList();
        if (slow.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("No completed sales in the last 30 days, with stock still on hand:");
            foreach (var row in slow)
                sb.AppendLine($"- {row.Name} ({row.Sku}): {row.CurrentStock:N0} on hand, about {row.CurrentStock * row.CostPrice:C} at cost.");
        }

        var fast = unitsLast30Days
            .OrderByDescending(p => p.Value)
            .Take(5)
            .Select(p => active.FirstOrDefault(r => r.ProductId == p.Key))
            .Where(r => r is not null)
            .Cast<InventoryRow>()
            .ToList();
        if (fast.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Highest unit sales in the last 30 days (completed and non-cancelled orders only):");
            foreach (var row in fast)
            {
                var units = unitsLast30Days[row.ProductId];
                sb.AppendLine($"- {row.Name} ({row.Sku}): {units:N0} units. Average {units / 30m:N1} per day. This rate is historical.");
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("There are no completed sales in the last 30 days, so I can't estimate days of stock remaining.");
        }

        return sb.ToString().Trim();
    }

    internal sealed record InventoryRow(
        Guid ProductId,
        string Name,
        string Sku,
        decimal CurrentStock,
        decimal ReorderLevel,
        decimal CostPrice,
        bool IsActive);
}

public sealed class GetCustomerInsightsTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetCustomerInsightsTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetCustomerInsights;
    public override string Description =>
        "Customer insights: best customers by non-cancelled order value, new customers this month, customers with outstanding invoice balances, and customers with no recent purchase.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.BusinessIntelligence or AiCopilotIntent.FollowUp
        && ContainsAny(message, "best customer", "top customer", "haven't purchased", "outstanding", "owes");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var zone = await BusinessClock.LoadAsync(_context, cancellationToken);
        var (start, end, label) = BusinessClock.Resolve(context.Message ?? "", zone);
        var summary = await BuildAsync(_context, start, end, label, cancellationToken);
        return new AiToolResult { ToolName = ToolName.ToString(), Summary = summary };
    }

    internal static async Task<string> BuildAsync(
        IApplicationDbContext context,
        DateTime start,
        DateTime end,
        string label,
        CancellationToken cancellationToken)
    {
        var top = await context.Orders.AsNoTracking()
            .Where(o => o.Status != OrderStatusNames.Cancelled)
            .GroupBy(o => new { o.CustomerId, o.Customer.FirstName, o.Customer.LastName })
            .Select(g => new
            {
                Name = (g.Key.FirstName + " " + g.Key.LastName).Trim(),
                Orders = g.Count(),
                Total = g.Sum(o => o.GrandTotal)
            })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToListAsync(cancellationToken);

        var created = await context.Customers.AsNoTracking()
            .CountAsync(c => c.CreatedAt >= start && c.CreatedAt <= end, cancellationToken);

        var invoices = await context.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatusNames.Cancelled && i.Status != InvoiceStatusNames.Draft)
            .Select(i => new
            {
                i.OrderId,
                i.GrandTotal,
                i.DueDate,
                i.Status,
                Customer = (i.Customer.FirstName + " " + i.Customer.LastName).Trim()
            })
            .Take(300)
            .ToListAsync(cancellationToken);

        var paid = await InvoicePaymentCalculator.GetAmountPaidByOrderIdsAsync(
            context, invoices.Select(i => i.OrderId).Distinct().ToList(), cancellationToken);
        var now = DateTime.UtcNow;
        var owed = invoices
            .Select(i =>
            {
                var amountPaid = paid.TryGetValue(i.OrderId, out var value) ? value : 0m;
                return new
                {
                    i.Customer,
                    Outstanding = InvoicePaymentCalculator.Outstanding(i.GrandTotal, amountPaid),
                    Overdue = InvoicePaymentCalculator.IsCollectibleOverdue(i.Status, InvoicePaymentCalculator.Outstanding(i.GrandTotal, amountPaid), i.DueDate, now)
                };
            })
            .Where(x => x.Outstanding > 0)
            .GroupBy(x => x.Customer)
            .Select(g => new { Customer = g.Key, Outstanding = g.Sum(x => x.Outstanding), Overdue = g.Any(x => x.Overdue) })
            .OrderByDescending(x => x.Outstanding)
            .Take(5)
            .ToList();

        var recentBuyerIds = await context.Orders.AsNoTracking()
            .Where(o => o.Status != OrderStatusNames.Cancelled && o.OrderDate >= DateTime.UtcNow.AddDays(-60))
            .Select(o => o.CustomerId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var dormant = await context.Customers.AsNoTracking()
            .Where(c => !recentBuyerIds.Contains(c.Id))
            .OrderBy(c => c.FirstName)
            .Take(5)
            .Select(c => (c.FirstName + " " + c.LastName).Trim())
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine(top.Count == 0
            ? "No non-cancelled orders yet, so I can't rank customers by purchase value."
            : "Best customers by order value (cancelled orders excluded):");
        foreach (var (row, index) in top.Select((row, index) => (row, index)))
            sb.AppendLine($"{index + 1}. {row.Name} — {row.Total:C} across {row.Orders:N0} order(s).");

        sb.AppendLine();
        sb.AppendLine($"New customers created {label}: {created:N0}.");

        sb.AppendLine();
        if (owed.Count == 0)
            sb.AppendLine("No customer currently has an outstanding balance on a non-draft, non-cancelled invoice.");
        else
        {
            sb.AppendLine("Largest outstanding invoice balances:");
            foreach (var row in owed)
                sb.AppendLine($"- {row.Customer}: {row.Outstanding:C} outstanding{(row.Overdue ? " (includes an overdue invoice)" : "")}.");
        }

        sb.AppendLine();
        sb.AppendLine(dormant.Count == 0
            ? "Every customer has a non-cancelled order in the last 60 days, or there are no customers."
            : "No non-cancelled purchase in the last 60 days: " + string.Join(", ", dormant) + ".");

        return sb.ToString().Trim();
    }
}

public sealed class GetDailyPrioritiesTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetDailyPrioritiesTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetDailyPriorities;
    public override string Description =>
        "Today's priorities from live data: overdue invoices, low stock, pending orders, quiet customers, open tasks, and employee count. Only includes measures the business records.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.DashboardInsight
        || ContainsAny(message, "focus today", "priorit", "how many employees");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var summary = await BuildAsync(_context, context.Message ?? "", cancellationToken);
        return new AiToolResult { ToolName = ToolName.ToString(), Summary = summary };
    }

    internal static async Task<string> BuildAsync(IApplicationDbContext context, string message, CancellationToken cancellationToken)
    {
        var employees = await context.Employees.AsNoTracking().CountAsync(e => e.IsActive, cancellationToken);
        var pendingTasks = await context.WorkTasks.AsNoTracking()
            .CountAsync(t => t.Status == WorkTaskStatus.Todo || t.Status == WorkTaskStatus.InProgress, cancellationToken);
        var assignedTasks = await context.WorkTasks.AsNoTracking()
            .CountAsync(t => (t.Status == WorkTaskStatus.Todo || t.Status == WorkTaskStatus.InProgress)
                && t.AssignedUserId != null, cancellationToken);
        var pendingOrders = await context.Orders.AsNoTracking()
            .CountAsync(o => o.Status == OrderStatusNames.Pending
                || o.Status == OrderStatusNames.Confirmed
                || o.Status == OrderStatusNames.Processing, cancellationToken);

        var low = await context.Inventories.AsNoTracking()
            .Where(i => i.CurrentStock > 0 && i.ReorderLevel > 0 && i.CurrentStock <= i.ReorderLevel)
            .OrderBy(i => i.CurrentStock)
            .Take(5)
            .Select(i => new { i.Product.Name, i.Product.SKU, i.CurrentStock, i.ReorderLevel })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var invoiceRows = await context.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatusNames.Cancelled && i.Status != InvoiceStatusNames.Draft && i.DueDate < now)
            .OrderBy(i => i.DueDate)
            .Take(40)
            .Select(i => new { i.InvoiceNumber, i.OrderId, i.GrandTotal, i.DueDate, i.Status, Customer = (i.Customer.FirstName + " " + i.Customer.LastName).Trim() })
            .ToListAsync(cancellationToken);
        var paid = await InvoicePaymentCalculator.GetAmountPaidByOrderIdsAsync(
            context, invoiceRows.Select(i => i.OrderId).Distinct().ToList(), cancellationToken);
        var overdue = invoiceRows
            .Select(i =>
            {
                var amountPaid = paid.TryGetValue(i.OrderId, out var value) ? value : 0m;
                var outstanding = InvoicePaymentCalculator.Outstanding(i.GrandTotal, amountPaid);
                return new { i.InvoiceNumber, i.Customer, i.DueDate, Outstanding = outstanding, IsOverdue = InvoicePaymentCalculator.IsCollectibleOverdue(i.Status, outstanding, i.DueDate, now) };
            })
            .Where(i => i.IsOverdue)
            .Take(5)
            .ToList();

        if (message.Contains("employee", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("focus", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("priorit", StringComparison.OrdinalIgnoreCase))
        {
            return $"Active employees on record: {employees:N0}. Open tasks: {pendingTasks:N0}, of which {assignedTasks:N0} have someone assigned. This count is the employee roster, not user logins.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Priorities from the current records, highest urgency first:");
        var rank = 1;
        if (overdue.Count > 0)
        {
            sb.AppendLine($"{rank++}. Collect overdue invoices ({overdue.Count} shown).");
            foreach (var row in overdue)
                sb.AppendLine($"   {row.InvoiceNumber} — {row.Customer}, due {row.DueDate:yyyy-MM-dd}, outstanding {row.Outstanding:C}.");
        }
        else
        {
            sb.AppendLine($"{rank++}. No overdue invoices met the overdue rule (past due, unpaid, and not draft, paid, or cancelled).");
        }

        if (low.Count > 0)
        {
            sb.AppendLine($"{rank++}. Replenish low stock.");
            foreach (var row in low)
                sb.AppendLine($"   {row.Name} ({row.SKU}): {row.CurrentStock:N0} available, reorder at {row.ReorderLevel:N0}.");
        }
        else
        {
            sb.AppendLine($"{rank++}. No product is above zero and at or below its reorder level.");
        }

        sb.AppendLine($"{rank++}. Pending orders (not completed or cancelled): {pendingOrders:N0}.");
        sb.AppendLine($"{rank++}. Open tasks: {pendingTasks:N0}. Assigned: {assignedTasks:N0}.");
        sb.AppendLine($"{rank}. Active employees on the roster: {employees:N0}.");
        sb.Append("I did not add a sales total here. Ask for revenue or sales if you want those figures.");
        return sb.ToString().Trim();
    }
}

public sealed class GetBusinessHealthTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetBusinessHealthTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetBusinessHealth;
    public override string Description =>
        "A business health brief from live inventory, sales, invoiced revenue, collections, overdue invoices, customers, and tasks. Missing measures are named instead of estimated.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "business health", "health of", "doing well", "how is my business", "how is our business");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var zone = await BusinessClock.LoadAsync(_context, cancellationToken);
        var (start, end, label) = BusinessClock.Resolve("this month", zone);
        var previous = BusinessClock.Resolve("last month", zone);

        var inventory = await GetInventoryHealthTool.LoadInventoryAsync(_context, cancellationToken);
        var salesByProduct = await GetInventoryHealthTool.LoadRecentSalesAsync(_context, cancellationToken);

        var orders = await _context.Orders.AsNoTracking()
            .Where(o => o.Status != OrderStatusNames.Cancelled && o.OrderDate >= start && o.OrderDate <= end)
            .Select(o => new { o.GrandTotal })
            .ToListAsync(cancellationToken);
        var priorOrders = await _context.Orders.AsNoTracking()
            .CountAsync(o => o.Status != OrderStatusNames.Cancelled && o.OrderDate >= previous.Start && o.OrderDate <= previous.End, cancellationToken);

        var invoiced = await _context.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end
                && i.Status != InvoiceStatusNames.Cancelled && i.Status != InvoiceStatusNames.Draft)
            .SumAsync(i => (decimal?)i.GrandTotal, cancellationToken) ?? 0m;
        var collected = await _context.Payments.AsNoTracking()
            .Where(p => p.PaymentDate >= start && p.PaymentDate <= end
                && (p.Status == PaymentStatusNames.Completed || p.Status == PaymentStatusNames.Refunded))
            .SumAsync(p => (decimal?)(p.Amount - p.RefundedAmount), cancellationToken) ?? 0m;

        var lines = await _context.OrderItems.AsNoTracking()
            .Where(oi => !oi.IsDeleted
                && oi.Order.Status != OrderStatusNames.Cancelled
                && oi.Order.OrderDate >= start && oi.Order.OrderDate <= end)
            .Select(oi => new { oi.Quantity, oi.Total, Cost = oi.Product != null ? oi.Product.CostPrice : 0m, HasCost = oi.Product != null && oi.Product.CostPrice > 0 })
            .ToListAsync(cancellationToken);

        var customerSummary = await GetCustomerInsightsTool.BuildAsync(_context, start, end, label, cancellationToken);
        var priorities = await GetDailyPrioritiesTool.BuildAsync(_context, "health", cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Business health for {label}. Figures below are calculated from this business's records.");
        sb.AppendLine();
        sb.AppendLine("1. Inventory");
        sb.AppendLine(GetInventoryHealthTool.BuildInventorySummary(inventory, salesByProduct));
        sb.AppendLine();
        sb.AppendLine("2. Sales");
        sb.AppendLine($"Non-cancelled orders {label}: {orders.Count:N0}, order value {orders.Sum(o => o.GrandTotal):C}. Last month had {priorOrders:N0} non-cancelled order(s). Cancelled orders are excluded. This is order value, not invoiced revenue and not profit.");
        if (orders.Count == 0 && priorOrders == 0)
            sb.AppendLine("There is not enough order history to say whether sales are rising or falling.");
        sb.AppendLine();
        sb.AppendLine("3. Revenue and cash");
        sb.AppendLine($"Invoiced revenue {label}: {invoiced:C}. Payments collected {label}: {collected:C}. These are different numbers.");
        if (lines.Count == 0 || lines.All(l => !l.HasCost))
            sb.AppendLine("Profit cannot be calculated. Completed sales lines have no product cost price, so I will not treat revenue as profit.");
        else
        {
            var estimated = lines.Sum(l => l.Total - (l.Cost * l.Quantity));
            sb.AppendLine($"Estimated gross profit on non-cancelled order lines {label}: {estimated:C} (selling total minus cost price times quantity). This is an estimate, not an audited profit figure. Expenses are not deducted.");
        }
        sb.AppendLine();
        sb.AppendLine("4. Customers");
        sb.AppendLine(customerSummary);
        sb.AppendLine();
        sb.AppendLine("5. What needs attention");
        sb.Append(priorities);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Summary = sb.ToString().Trim()
        };
    }
}

using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Copilot.Tools;

public abstract class AiToolBase : IAiTool
{
    public abstract AiToolName ToolName { get; }
    public abstract string Description { get; }
    public virtual IReadOnlyList<string> RequiredPermissions => [];

    public abstract bool CanHandle(
        AiCopilotIntent intent,
        string message,
        AiPageContextDto page,
        AiMemoryStateDto memory);

    public abstract Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default);

    protected static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    protected static (DateTime Start, DateTime End, string Label) ResolveDateRange(string message)
    {
        var now = DateTime.UtcNow;
        if (ContainsAny(message, "last year", "previous year"))
        {
            var start = new DateTime(now.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddYears(1).AddTicks(-1), "last year");
        }

        if (ContainsAny(message, "this year", "year"))
        {
            var start = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, now, "this year");
        }

        if (ContainsAny(message, "last month", "previous month"))
        {
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
            return (start, start.AddMonths(1).AddTicks(-1), "last month");
        }

        if (ContainsAny(message, "this quarter", "quarter"))
        {
            var quarter = (now.Month - 1) / 3;
            var start = new DateTime(now.Year, quarter * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, now, "this quarter");
        }

        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (monthStart, now, "this month");
    }
}

public sealed class GetCustomersTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetCustomersTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetCustomers;
    public override string Description => "Retrieve customers, top customers by revenue, or customer activity summaries.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.BusinessIntelligence or AiCopilotIntent.Analytics or AiCopilotIntent.FollowUp
        && (ContainsAny(message, "customer", "top", "revenue", "activity", "summarize") || page.CustomerId is not null || memory.SelectedCustomerId is not null);

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var customerId = context.Page.CustomerId ?? context.Memory.SelectedCustomerId;
        var (start, end, label) = ResolveDateRange(context.Message);

        if (customerId is not null)
        {
            var customer = await _context.Customers
                .Where(c => c.Id == customerId)
                .Select(c => new { c.Id, c.FirstName, c.LastName, c.Email, c.IsActive })
                .FirstOrDefaultAsync(cancellationToken);

            if (customer is null)
                return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Customer not found." };

            var revenue = await _context.Invoices
                .Where(i => i.CustomerId == customerId && i.InvoiceDate >= start && i.InvoiceDate <= end)
                .SumAsync(i => i.GrandTotal, cancellationToken);

            var unpaid = await _context.Invoices
                .Where(i => i.CustomerId == customerId && i.OutstandingAmount > 0)
                .CountAsync(cancellationToken);

            var name = $"{customer.FirstName} {customer.LastName}".Trim();
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = new { customer, revenue, unpaid, period = label },
                Summary = $"{name} generated {revenue:C} in revenue {label} with {unpaid} unpaid invoice(s)."
            };
        }

        if (ContainsAny(context.Message, "top", "best", "highest"))
        {
            var top = await _context.Invoices
                .Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end)
                .GroupBy(i => new { i.CustomerId, i.Customer.FirstName, i.Customer.LastName })
                .Select(g => new
                {
                    g.Key.CustomerId,
                    Name = (g.Key.FirstName + " " + g.Key.LastName).Trim(),
                    Revenue = g.Sum(x => x.GrandTotal)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(10)
                .ToListAsync(cancellationToken);

            var lines = top.Select((t, i) => $"{i + 1}. {t.Name}: {t.Revenue:C}");
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = top,
                Summary = top.Count == 0
                    ? $"No customer revenue recorded {label}."
                    : $"Top customers by revenue {label}:\n{string.Join("\n", lines)}"
            };
        }

        var count = await _context.Customers.CountAsync(cancellationToken);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { totalCustomers = count },
            Summary = $"You have {count:N0} customer(s) in your workspace."
        };
    }
}

public sealed class GetProjectsTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetProjectsTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetProjects;
    public override string Description => "Retrieve projects/orders, progress, and delayed projects.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "project", "order", "behind schedule", "delayed", "progress");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (ContainsAny(context.Message, "behind schedule", "delayed", "late"))
        {
            var delayedOrders = await _context.Orders
                .Where(o => o.Status != "Completed" && o.Status != "Cancelled" && o.CreatedAt < DateTime.UtcNow.AddDays(-30))
                .OrderBy(o => o.CreatedAt)
                .Take(10)
                .Select(o => new { o.OrderNumber, o.Status, o.GrandTotal, o.CreatedAt })
                .ToListAsync(cancellationToken);

            var delayedProjects = await _context.Projects
                .Where(p => p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled)
                .Where(p => p.Tasks.Any(t => t.DueDate < DateTime.UtcNow && t.Status != WorkTaskStatus.Done))
                .Take(10)
                .Select(p => new { p.Name, Status = p.Status.ToString(), OverdueTasks = p.Tasks.Count(t => t.DueDate < DateTime.UtcNow && t.Status != WorkTaskStatus.Done) })
                .ToListAsync(cancellationToken);

            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = new { delayedOrders, delayedProjects },
                Summary = delayedOrders.Count + delayedProjects.Count == 0
                    ? "No projects appear behind schedule."
                    : $"{delayedOrders.Count} order(s) and {delayedProjects.Count} project(s) may be behind schedule."
            };
        }

        var active = await _context.Orders
            .Where(o => o.Status != "Completed" && o.Status != "Cancelled")
            .CountAsync(cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { activeProjects = active },
            Summary = $"You have {active:N0} active project(s)/order(s)."
        };
    }
}

public sealed class GetTasksTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetTasksTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetTasks;
    public override string Description => "Retrieve pending, overdue, and team tasks.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "task", "todo", "pending", "delayed", "workload");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var overdue = await _context.WorkTasks
            .Where(t => t.DueDate < now && t.Status != WorkTaskStatus.Done && t.Status != WorkTaskStatus.Cancelled)
            .Take(10)
            .Select(t => new { t.Title, t.Status, t.DueDate, Project = t.Project.Name })
            .ToListAsync(cancellationToken);

        var pending = await _context.WorkTasks
            .CountAsync(t => t.Status == WorkTaskStatus.Todo || t.Status == WorkTaskStatus.InProgress, cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { overdue, pendingCount = pending },
            Summary = overdue.Count == 0
                ? $"You have {pending:N0} pending task(s). None are overdue."
                : $"{overdue.Count} task(s) are overdue. {pending:N0} task(s) still pending."
        };
    }
}

public sealed class GetInvoicesTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetInvoicesTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetInvoices;
    public override string Description => "Retrieve invoices, overdue/unpaid invoices, and payment status.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "invoice", "overdue", "unpaid", "outstanding", "payment");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var customerId = context.Page.CustomerId ?? context.Memory.SelectedCustomerId;

        var query = _context.Invoices
            .Where(i => i.OutstandingAmount > 0 && i.DueDate < now);

        if (customerId is not null)
            query = query.Where(i => i.CustomerId == customerId);

        var overdue = await query
            .OrderBy(i => i.DueDate)
            .Take(15)
            .Select(i => new
            {
                i.InvoiceNumber,
                Customer = (i.Customer.FirstName + " " + i.Customer.LastName).Trim(),
                i.DueDate,
                i.OutstandingAmount,
                i.Status
            })
            .ToListAsync(cancellationToken);

        var totalOutstanding = overdue.Sum(i => i.OutstandingAmount);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = overdue,
            Summary = overdue.Count == 0
                ? "No overdue invoices found."
                : $"{overdue.Count} overdue invoice(s) totaling {totalOutstanding:C} outstanding."
        };
    }
}

public sealed class GetExpensesTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetExpensesTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetExpenses;
    public override string Description => "Retrieve expenses, spending trends, and expense increases.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "expense", "spending", "cost");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var (thisStart, thisEnd, _) = ResolveDateRange("this month");
        var lastStart = thisStart.AddMonths(-1);
        var lastEnd = thisStart.AddTicks(-1);

        var thisMonth = await _context.Expenses
            .Where(e => e.ExpenseDate >= thisStart && e.ExpenseDate <= thisEnd)
            .SumAsync(e => e.Amount, cancellationToken);

        var lastMonth = await _context.Expenses
            .Where(e => e.ExpenseDate >= lastStart && e.ExpenseDate <= lastEnd)
            .SumAsync(e => e.Amount, cancellationToken);

        var change = lastMonth == 0 ? 0 : ((thisMonth - lastMonth) / lastMonth) * 100;

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { thisMonth, lastMonth, changePercent = change },
            Summary = $"Expenses this month: {thisMonth:C}. Last month: {lastMonth:C}. Change: {change:+0.0;-0.0;0}%."
        };
    }
}

public sealed class GetProductsTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetProductsTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetProducts;
    public override string Description => "Retrieve product catalog and inventory summaries.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "product", "catalog", "inventory");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var count = await _context.Products.CountAsync(cancellationToken);
        var lowStock = await _context.Inventories.CountAsync(i => i.CurrentStock <= i.ReorderLevel, cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { totalProducts = count, lowStockItems = lowStock },
            Summary = $"You have {count:N0} product(s). {lowStock:N0} item(s) at or below reorder level."
        };
    }
}

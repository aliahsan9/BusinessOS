using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Expenses.Commands.CreateExpense;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Application.Features.Orders.Commands.UpdateOrderStatus;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using BusinessOS.Infrastructure.AI.Agents.Tools;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Copilot.Tools;

public sealed class GetOrdersTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetOrdersTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetOrders;
    public override string Description => "List recent sales orders for the current tenant.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.OrderView];

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionRead or AiCopilotIntent.BusinessIntelligence
        && ContainsAny(message, "orders", "order list", "recent orders", "my orders")
        && !ContainsAny(message, "purchase order", "pending order", "order status");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Orders.AsNoTracking()
            .OrderByDescending(o => o.OrderDate)
            .Take(20)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.OrderDate,
                o.Status,
                Customer = (o.Customer.FirstName + " " + o.Customer.LastName).Trim(),
                o.GrandTotal
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = Array.Empty<object>(),
                Summary = "No sales orders found for this business."
            };
        }

        var lines = rows.Select(r =>
            $"{r.OrderNumber} — {r.Customer}, {r.OrderDate:yyyy-MM-dd}, {r.Status}, {r.GrandTotal:C}");
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = rows,
            Summary = $"Latest {rows.Count} order(s):\n" + string.Join("\n", lines)
        };
    }
}

public sealed class GetOrderDetailsTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetOrderDetailsTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetOrderDetails;
    public override string Description => "Get one sales order by number or selected order context.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.OrderView];

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        page.OrderId is not null
        || memory.SelectedOrderId is not null
        || ContainsAny(message, "order detail", "order number", "show order", "order status");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var orderId = context.Page.OrderId ?? context.Memory.SelectedOrderId;
        var message = context.Message ?? "";

        var query = _context.Orders.AsNoTracking().AsQueryable();
        if (orderId is Guid id)
            query = query.Where(o => o.Id == id);
        else
        {
            var number = ExtractOrderNumber(message);
            if (string.IsNullOrWhiteSpace(number))
            {
                return new AiToolResult
                {
                    ToolName = ToolName.ToString(),
                    Summary = "Which order should I open? Provide an order number or open an order page first."
                };
            }

            query = query.Where(o => o.OrderNumber == number);
        }

        var order = await query
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.OrderDate,
                o.Status,
                Customer = (o.Customer.FirstName + " " + o.Customer.LastName).Trim(),
                o.GrandTotal,
                ItemCount = o.OrderItems.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Summary = "I could not find that order in this business."
            };
        }

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = order,
            Summary = $"Order {order.OrderNumber}: {order.Customer}, {order.OrderDate:yyyy-MM-dd}, status {order.Status}, {order.ItemCount} line(s), total {order.GrandTotal:C}."
        };
    }

    private static string? ExtractOrderNumber(string message)
    {
        var tokens = message.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.FirstOrDefault(t => t.StartsWith("ORD", StringComparison.OrdinalIgnoreCase)
            || t.Contains('-'));
    }
}

public sealed class GetPendingOrdersTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetPendingOrdersTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetPendingOrders;
    public override string Description => "List pending sales orders for the current tenant.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.OrderView];

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "pending order", "open order", "orders pending", "waiting orders");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatusNames.Pending)
            .OrderByDescending(o => o.OrderDate)
            .Take(25)
            .Select(o => new
            {
                o.OrderNumber,
                o.OrderDate,
                Customer = (o.Customer.FirstName + " " + o.Customer.LastName).Trim(),
                o.GrandTotal
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = Array.Empty<object>(),
                Summary = "There are no pending sales orders."
            };
        }

        var lines = rows.Select(r =>
            $"{r.OrderNumber} — {r.Customer}, {r.OrderDate:yyyy-MM-dd}, {r.GrandTotal:C}");
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = rows,
            Summary = $"{rows.Count} pending order(s):\n" + string.Join("\n", lines)
        };
    }
}

public sealed class GetSaleDetailsTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetSaleDetailsTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetSaleDetails;
    public override string Description => "Show line-item detail for a recent sale/order.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.OrderView];

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "sale detail", "sale details", "what was sold", "order items", "line items");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var orderId = context.Page.OrderId ?? context.Memory.SelectedOrderId;
        var order = await _context.Orders.AsNoTracking()
            .Where(o => orderId == null || o.Id == orderId)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.OrderNumber,
                o.OrderDate,
                o.Status,
                Customer = (o.Customer.FirstName + " " + o.Customer.LastName).Trim(),
                Items = o.OrderItems.Select(i => new
                {
                    Product = i.Product != null ? i.Product.Name : "Product",
                    i.Quantity,
                    i.UnitPrice,
                    LineTotal = i.Total
                }).ToList(),
                o.GrandTotal
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Summary = "No sale found to detail."
            };
        }

        var lines = order.Items.Select(i => $"{i.Product} × {i.Quantity:N0} @ {i.UnitPrice:C} = {i.LineTotal:C}");
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = order,
            Summary = $"Sale {order.OrderNumber} ({order.OrderDate:yyyy-MM-dd}) for {order.Customer}, status {order.Status}, total {order.GrandTotal:C}.\n"
                + string.Join("\n", lines)
        };
    }
}

public sealed class GetStockHistoryTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetStockHistoryTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetStockHistory;
    public override string Description => "Recent inventory change audit entries for products.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.InventoryView];

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "stock history", "inventory history", "stock movement", "stock movements", "who changed stock");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var logs = await _context.EntityAuditLogs.AsNoTracking()
            .Where(l => l.EntityType == "Inventory" || l.EntityType == "Product")
            .OrderByDescending(l => l.ChangedAt)
            .Take(25)
            .Select(l => new
            {
                l.EntityType,
                l.EntityId,
                l.Action,
                l.ChangedBy,
                l.ChangedAt,
                l.NewValues
            })
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
        {
            var recent = await _context.Inventories.AsNoTracking()
                .OrderByDescending(i => i.LastUpdated)
                .Take(15)
                .Select(i => new
                {
                    Product = i.Product.Name,
                    i.CurrentStock,
                    i.LastUpdated
                })
                .ToListAsync(cancellationToken);

            if (recent.Count == 0)
            {
                return new AiToolResult
                {
                    ToolName = ToolName.ToString(),
                    Summary = "No stock movement history is recorded yet."
                };
            }

            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = recent,
                Summary = "No detailed movement log found. Recently updated stock levels:\n"
                    + string.Join("\n", recent.Select(r => $"{r.Product}: {r.CurrentStock:N0} (updated {r.LastUpdated:yyyy-MM-dd HH:mm} UTC)"))
            };
        }

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = logs,
            Summary = "Recent stock-related audit entries:\n"
                + string.Join("\n", logs.Select(l =>
                    $"{l.ChangedAt:yyyy-MM-dd HH:mm} UTC — {l.EntityType} {l.Action} by {l.ChangedBy}"))
        };
    }
}

public sealed class GetCustomerPurchaseHistoryTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetCustomerPurchaseHistoryTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetCustomerPurchaseHistory;
    public override string Description => "Purchase history for a customer in the current tenant.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.CustomerView, PermissionCodes.OrderView];

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "purchase history", "bought", "customer orders", "what did", "customer history");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var customerId = context.Page.CustomerId ?? context.Memory.SelectedCustomerId;
        var query = _context.Orders.AsNoTracking().AsQueryable();
        if (customerId is Guid id)
            query = query.Where(o => o.CustomerId == id);

        var rows = await query
            .OrderByDescending(o => o.OrderDate)
            .Take(20)
            .Select(o => new
            {
                o.OrderNumber,
                o.OrderDate,
                o.Status,
                Customer = (o.Customer.FirstName + " " + o.Customer.LastName).Trim(),
                o.GrandTotal
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Summary = "No purchase history found for that customer."
            };
        }

        var label = customerId is null ? "customers" : rows[0].Customer;
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = rows,
            Summary = $"Purchase history for {label}:\n"
                + string.Join("\n", rows.Select(r =>
                    $"{r.OrderNumber} — {r.OrderDate:yyyy-MM-dd}, {r.Status}, {r.GrandTotal:C}"))
        };
    }
}

public sealed class GetFinancialSummaryTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetFinancialSummaryTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetFinancialSummary;
    public override string Description => "Recorded revenue, expenses, and invoice outstanding totals for a period.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.FinanceView];

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "financial summary", "finance summary", "how much did i earn", "profit and loss", "p&l", "books");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-30);
        var revenue = await _context.Orders.AsNoTracking()
            .Where(o => o.OrderDate >= since && o.Status != OrderStatusNames.Cancelled)
            .SumAsync(o => (decimal?)o.GrandTotal, cancellationToken) ?? 0m;
        var expenses = await _context.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= since)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
        var invoiced = await _context.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= since && i.Status != "Cancelled" && i.Status != "Draft")
            .SumAsync(i => (decimal?)i.GrandTotal, cancellationToken) ?? 0m;

        var profit = revenue - expenses;
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = new { since, revenue, expenses, invoiced, profit },
            Summary = $"Last 30 days (recorded figures only): revenue {revenue:C}, expenses {expenses:C}, invoiced {invoiced:C}, profit {profit:C}."
        };
    }
}

public sealed class GetOutstandingPaymentsTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetOutstandingPaymentsTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetOutstandingPayments;
    public override string Description => "Outstanding invoice balances for the current tenant.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.InvoiceView];

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "outstanding payment", "outstanding payments", "who owes", "receivables", "unpaid invoices");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var invoices = await _context.Invoices.AsNoTracking()
            .Where(i => i.Status != "Cancelled" && i.Status != "Draft" && i.Status != "Paid")
            .OrderBy(i => i.DueDate)
            .Select(i => new
            {
                i.OrderId,
                i.InvoiceNumber,
                Customer = (i.Customer.FirstName + " " + i.Customer.LastName).Trim(),
                i.DueDate,
                i.GrandTotal,
                i.Status
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Summary = "No open invoices with collectible balances."
            };
        }

        var paid = await InvoicePaymentCalculator.GetAmountPaidByOrderIdsAsync(
            _context,
            invoices.Select(i => i.OrderId).Distinct().ToList(),
            cancellationToken);

        var open = invoices
            .Select(i =>
            {
                var amountPaid = paid.TryGetValue(i.OrderId, out var value) ? value : 0m;
                var outstanding = InvoicePaymentCalculator.Outstanding(i.GrandTotal, amountPaid);
                return new
                {
                    i.InvoiceNumber,
                    i.Customer,
                    i.DueDate,
                    i.Status,
                    Outstanding = outstanding,
                    Overdue = InvoicePaymentCalculator.IsCollectibleOverdue(i.Status, outstanding, i.DueDate, now)
                };
            })
            .Where(x => x.Outstanding > 0)
            .OrderBy(x => x.DueDate)
            .Take(20)
            .ToList();

        if (open.Count == 0)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Summary = "Every issued invoice is covered by recorded payments."
            };
        }

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = open,
            Summary = $"{open.Count} outstanding payment(s) totaling {open.Sum(x => x.Outstanding):C}.\n"
                + string.Join("\n", open.Select(x =>
                    $"{x.InvoiceNumber} — {x.Customer}, due {x.DueDate:yyyy-MM-dd}, {x.Outstanding:C}"
                    + (x.Overdue ? " (overdue)" : "")))
        };
    }
}

public sealed class CreateExpenseTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<CreateExpenseTool> _logger;

    public CreateExpenseTool(
        IMediator mediator,
        IApplicationDbContext db,
        ILogger<CreateExpenseTool> logger)
    {
        _mediator = mediator;
        _db = db;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.CreateExpense;
    public override string Description => "Create an expense after explicit confirmation.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.ExpenseCreate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.CreateExpense);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate
        && ContainsAny(message, "expense", "kharcha")
        && ContainsAny(message, "create", "add", "record", "log");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context,
        JsonElement args,
        CancellationToken cancellationToken = default)
    {
        if (context.ToolArgs is null && !args.EnumerateObject().Any())
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Summary = "That expense was not confirmed, so nothing was created."
            };
        }

        var title = AgentToolArgHelpers.Str(args, "title")
            ?? AgentToolArgHelpers.Str(args, "name");
        var amount = AgentToolArgHelpers.Dec(args, "amount");
        var categoryId = AgentToolArgHelpers.GuidVal(args, "expenseCategoryId")
            ?? AgentToolArgHelpers.GuidVal(args, "categoryId");

        if (string.IsNullOrWhiteSpace(title) || amount is null || amount <= 0)
        {
            return Fail("Expense needs a title and a positive amount. Nothing was created.");
        }

        if (categoryId is null)
        {
            var categories = await _db.ExpenseCategories.AsNoTracking()
                .OrderBy(c => c.Name)
                .Take(8)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync(cancellationToken);
            if (categories.Count == 1)
                categoryId = categories[0].Id;
            else if (categories.Count == 0)
                return Fail("There is no expense category yet. Create one in Finance first.");
            else
                return Fail("Which expense category should I use? " + string.Join(", ", categories.Select(c => c.Name)));
        }

        try
        {
            var id = await _mediator.Send(
                new CreateExpenseCommand(
                    title.Trim(),
                    amount.Value,
                    DateTime.UtcNow.Date,
                    categoryId.Value,
                    AgentToolArgHelpers.Str(args, "paymentMethod") ?? "Cash",
                    AgentToolArgHelpers.Str(args, "vendor"),
                    AgentToolArgHelpers.Str(args, "referenceNumber"),
                    AgentToolArgHelpers.Str(args, "description"),
                    null,
                    "Posted",
                    false,
                    null),
                cancellationToken);

            _logger.LogInformation("AI created expense {ExpenseId}", id);
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = new { expenseId = id, title, amount = amount.Value },
                Summary = $"Expense '{title}' for {amount.Value:C} was created.",
                ActionResult = new AiActionResultDto
                {
                    Success = true,
                    Action = "CreateExpense",
                    Message = $"Expense '{title}' for {amount.Value:C} was created.",
                    EntityType = "Expense",
                    EntityId = id
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreateExpense failed");
            return Fail("The expense was not created. " + ex.Message);
        }
    }

    private AiToolResult Fail(string summary) => new()
    {
        ToolName = ToolName.ToString(),
        Success = false,
        Summary = summary,
        ActionResult = new AiActionResultDto
        {
            Success = false,
            Action = "CreateExpense",
            Message = summary,
            EntityType = "Expense"
        }
    };
}

public sealed class UpdateOrderStatusTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<UpdateOrderStatusTool> _logger;

    public UpdateOrderStatusTool(
        IMediator mediator,
        IApplicationDbContext db,
        ILogger<UpdateOrderStatusTool> logger)
    {
        _mediator = mediator;
        _db = db;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.UpdateOrderStatus;
    public override string Description => "Update a sales order status after explicit confirmation.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.OrderUpdate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.UpdateOrderStatus);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate
        && ContainsAny(message, "order")
        && ContainsAny(message, "status", "complete", "cancel", "ship", "deliver");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context,
        JsonElement args,
        CancellationToken cancellationToken = default)
    {
        if (context.ToolArgs is null && !args.EnumerateObject().Any())
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Summary = "That order status change was not confirmed, so nothing was updated."
            };
        }

        var orderId = AgentToolArgHelpers.GuidVal(args, "orderId")
            ?? context.Page.OrderId
            ?? context.Memory.SelectedOrderId;
        var status = AgentToolArgHelpers.Str(args, "status");

        if (orderId is null || string.IsNullOrWhiteSpace(status))
            return Fail("I need the order and the new status. Nothing was changed.");

        var exists = await _db.Orders.AsNoTracking().AnyAsync(o => o.Id == orderId, cancellationToken);
        if (!exists)
            return Fail("That order was not found in this business.");

        try
        {
            await _mediator.Send(new UpdateOrderStatusCommand(orderId.Value, status.Trim()), cancellationToken);
            _logger.LogInformation("AI updated order {OrderId} to {Status}", orderId, status);
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = new { orderId, status },
                Summary = $"Order status updated to {status}.",
                ActionResult = new AiActionResultDto
                {
                    Success = true,
                    Action = "UpdateOrderStatus",
                    Message = $"Order status updated to {status}.",
                    EntityType = "Order",
                    EntityId = orderId
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UpdateOrderStatus failed");
            return Fail("The order status was not updated. " + ex.Message);
        }
    }

    private AiToolResult Fail(string summary) => new()
    {
        ToolName = ToolName.ToString(),
        Success = false,
        Summary = summary,
        ActionResult = new AiActionResultDto
        {
            Success = false,
            Action = "UpdateOrderStatus",
            Message = summary,
            EntityType = "Order"
        }
    };
}

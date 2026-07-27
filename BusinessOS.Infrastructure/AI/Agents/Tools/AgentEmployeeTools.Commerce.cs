using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Finance.Services;
using BusinessOS.Application.Features.Invoices.Commands.CreateInvoiceFromOrder;
using BusinessOS.Application.Features.Invoices.Commands.UpdateInvoiceStatus;
using BusinessOS.Application.Features.Orders.Commands.CreateOrder;
using BusinessOS.Application.Features.Orders.Queries;
using BusinessOS.Application.Features.PurchaseOrders.Commands.CreatePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Commands.ReceivePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Commands.UpdatePurchaseOrderStatus;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using BusinessOS.Application.Features.Settings.DTOs;
using BusinessOS.Application.Features.Settings.Services;
using BusinessOS.Application.Features.Suppliers.Commands.CreateSupplier;
using BusinessOS.Application.Features.Suppliers.Commands.DeleteSupplier;
using BusinessOS.Application.Features.Suppliers.Commands.UpdateSupplier;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using BusinessOS.Infrastructure.AI.Copilot.Tools;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents.Tools;

public sealed class CreateSaleTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<CreateSaleTool> _logger;

    public CreateSaleTool(IMediator mediator, IApplicationDbContext db, ILogger<CreateSaleTool> logger)
    {
        _mediator = mediator;
        _db = db;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.CreateSale;
    public override string Description => "Create a sales order.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.OrderCreate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.CreateSale);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate
        && ContainsAny(message, "sale", "sales order", "create order")
        && !ContainsAny(message, "purchase order");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        try
        {
            var customerId = AgentToolArgHelpers.GuidVal(args, "customerId")
                ?? context.ExecutionState?.CustomerId
                ?? context.Page.CustomerId
                ?? context.Memory.SelectedCustomerId;

            if (customerId is null)
            {
                var customerName = AgentToolArgHelpers.Str(args, "customerName");
                if (!string.IsNullOrWhiteSpace(customerName))
                {
                    var q = customerName.ToLowerInvariant();
                    customerId = await _db.Customers.AsNoTracking()
                        .Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(q))
                        .Select(c => (Guid?)c.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                }
            }

            if (customerId is null)
            {
                var refersToThis = ContainsAny(context.Message,
                    "this customer", "the customer");
                if (refersToThis)
                {
                    return Fail("Open the customer page first, then say \"Create order for this customer\" — or tell me the customer name.");
                }

                return Fail("Which customer is this for? Example: \"Create order for Ahmed Ali\".");
            }

            var items = new List<CreateOrderItemDto>();
            if (args.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsEl.EnumerateArray())
                {
                    var productId = AgentToolArgHelpers.GuidVal(item, "productId");
                    if (productId is null)
                    {
                        var pname = AgentToolArgHelpers.Str(item, "productName") ?? AgentToolArgHelpers.Str(item, "sku");
                        if (!string.IsNullOrWhiteSpace(pname))
                        {
                            var pq = pname.ToLowerInvariant();
                            productId = await _db.Products.AsNoTracking()
                                .Where(p => p.Name.ToLower().Contains(pq) || p.SKU.ToLower().Contains(pq))
                                .Select(p => (Guid?)p.Id)
                                .FirstOrDefaultAsync(cancellationToken);
                        }
                    }

                    var qty = AgentToolArgHelpers.Dec(item, "quantity") ?? 1;
                    if (productId is not null)
                        items.Add(new CreateOrderItemDto(productId.Value, qty));
                }
            }

            if (items.Count == 0 && context.ExecutionState?.ProductId is Guid pid)
                items.Add(new CreateOrderItemDto(pid, 1));

            if (items.Count == 0)
            {
                return Fail("Which product and how many? Example: \"Laptop quantity 5\".");
            }

            var orderId = await _mediator.Send(new CreateOrderCommand(
                customerId.Value,
                AgentToolArgHelpers.Dec(args, "discount") ?? 0,
                AgentToolArgHelpers.Dec(args, "tax") ?? 0,
                items), cancellationToken);

            if (context.ExecutionState is not null)
                context.ExecutionState.OrderId = orderId;

            return Ok(
                "Sale/order created successfully.",
                "Order",
                orderId,
                $"/orders/{orderId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreateSale failed");
            return Fail(ex.Message);
        }
    }

    private AiToolResult Fail(string m) => new() { ToolName = ToolName.ToString(), Success = false, Summary = m };
    private AiToolResult Ok(string m, string type, Guid id, string route) => new()
    {
        ToolName = ToolName.ToString(),
        Success = true,
        Summary = m,
        ActionResult = new AiActionResultDto
        {
            Action = ToolName.ToString(),
            Success = true,
            Message = m,
            EntityType = type,
            EntityId = id,
            Route = route
        }
    };
}

public sealed class StructuredCreateInvoiceTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<StructuredCreateInvoiceTool> _logger;

    public StructuredCreateInvoiceTool(IMediator mediator, IApplicationDbContext db, ILogger<StructuredCreateInvoiceTool> logger)
    {
        _mediator = mediator;
        _db = db;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.CreateInvoice;
    public override string Description => "Create an invoice from an order or for a customer.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.InvoiceCreate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.CreateInvoice);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate && ContainsAny(message, "invoice");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        try
        {
            var orderId = AgentToolArgHelpers.GuidVal(args, "orderId")
                ?? context.ExecutionState?.OrderId
                ?? context.Page.OrderId
                ?? context.Memory.SelectedOrderId;

            if (orderId is null)
            {
                var customerId = AgentToolArgHelpers.GuidVal(args, "customerId")
                    ?? context.ExecutionState?.CustomerId
                    ?? context.Page.CustomerId;

                if (customerId is null)
                {
                    var customerName = AgentToolArgHelpers.Str(args, "customerName");
                    if (!string.IsNullOrWhiteSpace(customerName))
                    {
                        var q = customerName.ToLowerInvariant();
                        customerId = await _db.Customers.AsNoTracking()
                            .Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(q))
                            .Select(c => (Guid?)c.Id)
                            .FirstOrDefaultAsync(cancellationToken);
                    }
                }

                if (customerId is not null)
                {
                    orderId = await _db.Orders.AsNoTracking()
                        .Where(o => o.CustomerId == customerId)
                        .OrderByDescending(o => o.CreatedAt)
                        .Select(o => (Guid?)o.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                }
            }

            if (orderId is null)
                return Fail("No order found to invoice. Create a sale first.");

            var dueDays = AgentToolArgHelpers.Int(args, "dueDays") ?? 14;
            var invoiceId = await _mediator.Send(new CreateInvoiceFromOrderCommand(
                orderId.Value,
                DateTime.UtcNow.AddDays(dueDays),
                AgentToolArgHelpers.Str(args, "notes")), cancellationToken);

            if (context.ExecutionState is not null)
                context.ExecutionState.InvoiceId = invoiceId;

            return Ok("Invoice created successfully.", "Invoice", invoiceId, $"/invoices/{invoiceId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreateInvoice failed");
            return Fail(ex.Message);
        }
    }

    private AiToolResult Fail(string m) => new() { ToolName = ToolName.ToString(), Success = false, Summary = m };
    private AiToolResult Ok(string m, string type, Guid id, string route) => new()
    {
        ToolName = ToolName.ToString(),
        Success = true,
        Summary = m,
        ActionResult = new AiActionResultDto
        {
            Action = "CreateInvoice",
            Success = true,
            Message = m,
            EntityType = type,
            EntityId = id,
            Route = route
        }
    };
}

public sealed class CancelInvoiceTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public CancelInvoiceTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.CancelInvoice;
    public override string Description => "Cancel an invoice.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.InvoiceUpdate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.CancelInvoice);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "cancel invoice", "void invoice");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "invoiceId")
            ?? context.ExecutionState?.InvoiceId
            ?? context.Page.InvoiceId;

        if (id is null)
        {
            var number = AgentToolArgHelpers.Str(args, "invoiceNumber");
            if (!string.IsNullOrWhiteSpace(number))
            {
                id = await _db.Invoices.AsNoTracking()
                    .Where(i => i.InvoiceNumber.Contains(number))
                    .Select(i => (Guid?)i.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (id is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Invoice not found." };

        await _mediator.Send(new UpdateInvoiceStatusCommand(id.Value, InvoiceStatusNames.Cancelled), cancellationToken);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = "Invoice cancelled.",
            ActionResult = new AiActionResultDto
            {
                Action = "CancelInvoice",
                Success = true,
                Message = "Invoice cancelled.",
                EntityType = "Invoice",
                EntityId = id,
                Route = $"/invoices/{id}"
            }
        };
    }
}

public sealed class SearchInvoiceTool : AiToolBase
{
    private readonly IApplicationDbContext _db;
    public SearchInvoiceTool(IApplicationDbContext db) => _db = db;

    public override AiToolName ToolName => AiToolName.SearchInvoice;
    public override string Description => "Search invoices.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.InvoiceView];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.SearchInvoice);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "search invoice", "find invoice");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var query = AgentToolArgHelpers.Str(args, "query")
            ?? AgentToolArgHelpers.Str(args, "invoiceNumber")
            ?? AgentToolArgHelpers.Str(args, "customerName")
            ?? context.Message;
        var q = query.Trim().ToLowerInvariant();

        var matches = await _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceNumber.ToLower().Contains(q))
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .Select(i => new { i.Id, i.InvoiceNumber, i.Status, i.GrandTotal })
            .ToListAsync(cancellationToken);

        if (matches.Count == 1 && context.ExecutionState is not null)
            context.ExecutionState.InvoiceId = matches[0].Id;

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = matches.Count > 0,
            Summary = matches.Count == 0
                ? $"No invoices found for \"{query}\"."
                : $"Found {matches.Count} invoice(s): " + string.Join("; ", matches.Select(m => $"{m.InvoiceNumber} ({m.Status})")),
            Data = matches
        };
    }
}

public sealed class CreatePurchaseOrderTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<CreatePurchaseOrderTool> _logger;

    public CreatePurchaseOrderTool(IMediator mediator, IApplicationDbContext db, ILogger<CreatePurchaseOrderTool> logger)
    {
        _mediator = mediator;
        _db = db;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.CreatePurchaseOrder;
    public override string Description => "Create a purchase order.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.PurchaseOrderCreate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.CreatePurchaseOrder);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "purchase order", "create po", "buy stock", "reorder", "order from supplier");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        try
        {
            var supplierId = AgentToolArgHelpers.GuidVal(args, "supplierId") ?? context.ExecutionState?.SupplierId;
            if (supplierId is null)
            {
                var sname = AgentToolArgHelpers.Str(args, "supplierName")
                    ?? context.ExecutionState?.SupplierName;
                if (!string.IsNullOrWhiteSpace(sname))
                {
                    var q = sname.ToLowerInvariant();
                    supplierId = await _db.Suppliers.AsNoTracking()
                        .Where(s => s.Name.ToLower().Contains(q))
                        .Select(s => (Guid?)s.Id)
                        .FirstOrDefaultAsync(cancellationToken);
                }
                supplierId ??= await _db.Suppliers.AsNoTracking()
                    .OrderBy(s => s.Name)
                    .Select(s => (Guid?)s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (supplierId is null)
                return Fail("No supplier found. Create a supplier first (e.g. \"Create supplier Acme Traders\"), then I can create the purchase order.");

            var items = new List<CreatePurchaseOrderLineItemDto>();
            if (args.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsEl.EnumerateArray())
                {
                    var line = await ResolveLineAsync(item, cancellationToken);
                    if (line is not null)
                        items.Add(line);
                }
            }

            // Top-level productName / quantity (from heuristics or clarification replies).
            if (items.Count == 0)
            {
                var topName = AgentToolArgHelpers.Str(args, "productName")
                    ?? context.ExecutionState?.ProductName;
                var topQty = AgentToolArgHelpers.Dec(args, "quantity") ?? 1m;
                var topId = AgentToolArgHelpers.GuidVal(args, "productId")
                    ?? context.ExecutionState?.ProductId;

                if (topId is Guid knownId)
                {
                    var cost = await _db.Products.AsNoTracking()
                        .Where(p => p.Id == knownId)
                        .Select(p => p.CostPrice)
                        .FirstOrDefaultAsync(cancellationToken);
                    items.Add(new CreatePurchaseOrderLineItemDto(knownId, topQty, cost));
                }
                else if (!string.IsNullOrWhiteSpace(topName))
                {
                    var resolved = await ResolveProductByNameAsync(topName, cancellationToken);
                    if (resolved is null)
                        return Fail($"I couldn't find a product named \"{topName}\". Check the name or create the product first.");
                    if (resolved.Value.Ambiguous)
                        return Fail($"Multiple products match \"{topName}\". Please be more specific (use exact name or SKU).");

                    items.Add(new CreatePurchaseOrderLineItemDto(
                        resolved.Value.Id,
                        topQty,
                        resolved.Value.CostPrice));

                    if (context.ExecutionState is not null)
                    {
                        context.ExecutionState.ProductId = resolved.Value.Id;
                        context.ExecutionState.ProductName = resolved.Value.Name;
                    }
                }
            }

            if (items.Count == 0 && context.ExecutionState?.ProductId is Guid pid)
            {
                var cost = await _db.Products.AsNoTracking()
                    .Where(p => p.Id == pid)
                    .Select(p => p.CostPrice)
                    .FirstOrDefaultAsync(cancellationToken);
                items.Add(new CreatePurchaseOrderLineItemDto(pid, 1, cost));
            }

            // Last resort: scan the raw message for a known product name.
            if (items.Count == 0 && !string.IsNullOrWhiteSpace(context.Message))
            {
                var fromMessage = await FindProductMentionedInMessageAsync(context.Message, cancellationToken);
                if (fromMessage is not null)
                {
                    items.Add(new CreatePurchaseOrderLineItemDto(
                        fromMessage.Value.Id, 1, fromMessage.Value.CostPrice));
                    if (context.ExecutionState is not null)
                    {
                        context.ExecutionState.ProductId = fromMessage.Value.Id;
                        context.ExecutionState.ProductName = fromMessage.Value.Name;
                    }
                }
            }

            if (items.Count == 0)
            {
                return Fail(
                    "Which product should I order, and how many? Example: \"Create purchase order for Laptop quantity 5\" — or say \"draft purchase order from low stock\" to auto-fill from inventory.");
            }

            var poId = await _mediator.Send(new CreatePurchaseOrderCommand(
                supplierId.Value,
                DateTime.UtcNow,
                PurchaseOrderStatusNames.Draft,
                null,
                "Created by Sophia",
                items), cancellationToken);

            if (context.ExecutionState is not null)
                context.ExecutionState.PurchaseOrderId = poId;

            var supplierLabel = await _db.Suppliers.AsNoTracking()
                .Where(s => s.Id == supplierId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "supplier";

            return Ok(
                $"Purchase order draft created for {supplierLabel} with {items.Count} line(s).",
                "PurchaseOrder",
                poId,
                $"/purchase-orders/{poId}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreatePurchaseOrder failed");
            return Fail(ex.Message);
        }
    }

    private async Task<CreatePurchaseOrderLineItemDto?> ResolveLineAsync(JsonElement item, CancellationToken cancellationToken)
    {
        var productId = AgentToolArgHelpers.GuidVal(item, "productId");
        string? resolvedName = null;
        decimal cost = AgentToolArgHelpers.Dec(item, "unitCost") ?? 0;

        if (productId is null)
        {
            var pname = AgentToolArgHelpers.Str(item, "productName");
            if (string.IsNullOrWhiteSpace(pname))
                return null;

            var resolved = await ResolveProductByNameAsync(pname, cancellationToken);
            if (resolved is null || resolved.Value.Ambiguous)
                return null;

            productId = resolved.Value.Id;
            resolvedName = resolved.Value.Name;
            if (cost <= 0)
                cost = resolved.Value.CostPrice;
        }
        else if (cost <= 0)
        {
            cost = await _db.Products.AsNoTracking()
                .Where(p => p.Id == productId.Value)
                .Select(p => p.CostPrice)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var qty = AgentToolArgHelpers.Dec(item, "quantity") ?? 1;
        _ = resolvedName;
        return new CreatePurchaseOrderLineItemDto(productId.Value, qty, cost);
    }

    private async Task<(Guid Id, string Name, decimal CostPrice, bool Ambiguous)?> ResolveProductByNameAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var q = name.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(q))
            return null;

        var matches = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && (p.Name.ToLower().Contains(q) || (p.SKU != null && p.SKU.ToLower().Contains(q))))
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.CostPrice })
            .Take(5)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
            return null;

        var exact = matches.FirstOrDefault(m => m.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return (exact.Id, exact.Name, exact.CostPrice, false);

        if (matches.Count > 1)
            return (matches[0].Id, matches[0].Name, matches[0].CostPrice, true);

        return (matches[0].Id, matches[0].Name, matches[0].CostPrice, false);
    }

    private async Task<(Guid Id, string Name, decimal CostPrice)?> FindProductMentionedInMessageAsync(
        string message,
        CancellationToken cancellationToken)
    {
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.Name.Length)
            .Select(p => new { p.Id, p.Name, p.CostPrice })
            .Take(200)
            .ToListAsync(cancellationToken);

        foreach (var p in products)
        {
            if (message.Contains(p.Name, StringComparison.OrdinalIgnoreCase))
                return (p.Id, p.Name, p.CostPrice);
        }

        return null;
    }

    private AiToolResult Fail(string m) => new() { ToolName = ToolName.ToString(), Success = false, Summary = m };
    private AiToolResult Ok(string m, string type, Guid id, string route) => new()
    {
        ToolName = ToolName.ToString(),
        Success = true,
        Summary = m,
        ActionResult = new AiActionResultDto
        {
            Action = "CreatePurchaseOrder",
            Success = true,
            Message = m,
            EntityType = type,
            EntityId = id,
            Route = route
        }
    };
}

public sealed class ApprovePurchaseOrderTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public ApprovePurchaseOrderTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.ApprovePurchaseOrder;
    public override string Description => "Approve a purchase order.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.PurchaseOrderUpdate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.ApprovePurchaseOrder);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "approve purchase", "approve po");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "purchaseOrderId") ?? context.ExecutionState?.PurchaseOrderId;
        if (id is null)
        {
            var po = AgentToolArgHelpers.Str(args, "poNumber");
            if (!string.IsNullOrWhiteSpace(po))
            {
                id = await _db.Purchases.AsNoTracking()
                    .Where(p => p.ReferenceNumber != null && p.ReferenceNumber.Contains(po))
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (id is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Purchase order not found." };

        await _mediator.Send(new UpdatePurchaseOrderStatusCommand(id.Value, PurchaseOrderStatusNames.Approved), cancellationToken);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = "Purchase order approved.",
            ActionResult = new AiActionResultDto
            {
                Action = "ApprovePurchaseOrder",
                Success = true,
                Message = "Purchase order approved.",
                EntityType = "PurchaseOrder",
                EntityId = id
            }
        };
    }
}

public sealed class ReceivePurchaseTool : AiToolBase
{
    private readonly IMediator _mediator;

    public ReceivePurchaseTool(IMediator mediator) => _mediator = mediator;

    public override AiToolName ToolName => AiToolName.ReceivePurchase;
    public override string Description => "Receive a purchase order into inventory.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.PurchaseOrderUpdate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.ReceivePurchase);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "receive purchase", "receive po");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "purchaseOrderId") ?? context.ExecutionState?.PurchaseOrderId;
        if (id is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Purchase order not found." };

        await _mediator.Send(new ReceivePurchaseOrderCommand(id.Value), cancellationToken);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = "Purchase order received into inventory.",
            ActionResult = new AiActionResultDto
            {
                Action = "ReceivePurchase",
                Success = true,
                Message = "Purchase order received into inventory.",
                EntityType = "PurchaseOrder",
                EntityId = id
            }
        };
    }
}

public sealed class SearchSupplierTool : AiToolBase
{
    private readonly IApplicationDbContext _db;
    public SearchSupplierTool(IApplicationDbContext db) => _db = db;

    public override AiToolName ToolName => AiToolName.SearchSupplier;
    public override string Description => "Search suppliers.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.SupplierView];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.SearchSupplier);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "search supplier", "find supplier");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var query = AgentToolArgHelpers.Str(args, "query") ?? context.Message;
        var q = query.Trim().ToLowerInvariant();
        var matches = await _db.Suppliers.AsNoTracking()
            .Where(s => s.Name.ToLower().Contains(q) || (s.Email != null && s.Email.ToLower().Contains(q)))
            .Take(10)
            .Select(s => new { s.Id, s.Name, s.Phone, s.Email })
            .ToListAsync(cancellationToken);

        if (matches.Count == 1 && context.ExecutionState is not null)
        {
            context.ExecutionState.SupplierId = matches[0].Id;
            context.ExecutionState.SupplierName = matches[0].Name;
        }

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = matches.Count > 0,
            Summary = matches.Count == 0
                ? $"No suppliers found for \"{query}\"."
                : $"Found {matches.Count} supplier(s): " + string.Join("; ", matches.Select(m => m.Name)),
            Data = matches
        };
    }
}

public sealed class CreateSupplierTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CreateSupplierTool> _logger;

    public CreateSupplierTool(IMediator mediator, ILogger<CreateSupplierTool> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.CreateSupplier;
    public override string Description => "Create a supplier.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.SupplierCreate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.CreateSupplier);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate && ContainsAny(message, "supplier");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        try
        {
            var name = AgentToolArgHelpers.Str(args, "name");
            if (string.IsNullOrWhiteSpace(name))
                return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Supplier name is required." };

            var id = await _mediator.Send(new CreateSupplierCommand(
                name,
                AgentToolArgHelpers.Str(args, "email") ?? $"supplier-{Guid.NewGuid().ToString("N")[..6]}@businessos.local",
                AgentToolArgHelpers.Str(args, "phone") ?? "+0000000000",
                AgentToolArgHelpers.Str(args, "address") ?? "N/A",
                AgentToolArgHelpers.Str(args, "contactPerson"),
                AgentToolArgHelpers.Str(args, "notes")), cancellationToken);

            if (context.ExecutionState is not null)
            {
                context.ExecutionState.SupplierId = id;
                context.ExecutionState.SupplierName = name;
            }

            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = true,
                Summary = $"Supplier \"{name}\" has been created successfully.",
                ActionResult = new AiActionResultDto
                {
                    Action = "CreateSupplier",
                    Success = true,
                    Message = $"Supplier \"{name}\" has been created successfully.",
                    EntityType = "Supplier",
                    EntityId = id,
                    Route = $"/suppliers/{id}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreateSupplier failed");
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = ex.Message };
        }
    }
}

public sealed class UpdateSupplierTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public UpdateSupplierTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.UpdateSupplier;
    public override string Description => "Update a supplier.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.SupplierUpdate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.UpdateSupplier);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "update supplier", "edit supplier");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "supplierId") ?? context.ExecutionState?.SupplierId;
        if (id is null)
        {
            var name = AgentToolArgHelpers.Str(args, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var q = name.ToLowerInvariant();
                id = await _db.Suppliers.AsNoTracking()
                    .Where(s => s.Name.ToLower().Contains(q))
                    .Select(s => (Guid?)s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (id is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Supplier not found." };

        var existing = await _db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == id, cancellationToken);
        await _mediator.Send(new UpdateSupplierCommand(
            id.Value,
            AgentToolArgHelpers.Str(args, "name") ?? existing.Name,
            AgentToolArgHelpers.Str(args, "email") ?? existing.Email,
            AgentToolArgHelpers.Str(args, "phone") ?? existing.Phone,
            AgentToolArgHelpers.Str(args, "address") ?? existing.Address,
            AgentToolArgHelpers.Str(args, "contactPerson") ?? existing.ContactPerson,
            AgentToolArgHelpers.Str(args, "notes") ?? existing.Notes), cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = $"Supplier \"{existing.Name}\" was updated.",
            ActionResult = new AiActionResultDto
            {
                Action = "UpdateSupplier",
                Success = true,
                Message = $"Supplier \"{existing.Name}\" was updated.",
                EntityType = "Supplier",
                EntityId = id
            }
        };
    }
}

public sealed class DeleteSupplierTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public DeleteSupplierTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.DeleteSupplier;
    public override string Description => "Delete a supplier.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.SupplierDelete];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.DeleteSupplier);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "delete supplier", "remove supplier");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "supplierId") ?? context.ExecutionState?.SupplierId;
        if (id is null)
        {
            var name = AgentToolArgHelpers.Str(args, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var q = name.ToLowerInvariant();
                id = await _db.Suppliers.AsNoTracking()
                    .Where(s => s.Name.ToLower().Contains(q))
                    .Select(s => (Guid?)s.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (id is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Supplier not found." };

        await _mediator.Send(new DeleteSupplierCommand(id.Value), cancellationToken);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = "Supplier deleted successfully.",
            ActionResult = new AiActionResultDto
            {
                Action = "DeleteSupplier",
                Success = true,
                Message = "Supplier deleted successfully.",
                EntityType = "Supplier",
                EntityId = id
            }
        };
    }
}

public sealed class ShowProfitTool : AiToolBase
{
    private readonly IFinanceService _finance;

    public ShowProfitTool(IFinanceService finance) => _finance = finance;

    public override AiToolName ToolName => AiToolName.ShowProfit;
    public override string Description => "Show profit and loss summary.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.FinanceView];

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "profit", "p&l", "show expenses", "expense summary");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var pl = await _finance.GetProfitLossAsync(null, null, "this_month", null, cancellationToken);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = $"This month — Revenue: {pl.TotalRevenue:N2}, Gross profit: {pl.GrossProfit:N2}, Net profit: {pl.NetProfit:N2}, Expenses: {pl.TotalExpenses:N2}.",
            Data = pl
        };
    }
}

public sealed class UpdateCompanyProfileTool : AiToolBase
{
    private readonly ISettingsService _settings;

    public UpdateCompanyProfileTool(ISettingsService settings) => _settings = settings;

    public override AiToolName ToolName => AiToolName.UpdateCompanyProfile;
    public override string Description => "Update company business profile.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.SettingsManage];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.UpdateCompanyProfile);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "update company", "company profile", "business name", "update business");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var current = await _settings.GetBusinessProfileAsync(cancellationToken);
        var updated = await _settings.UpdateBusinessProfileAsync(new UpdateBusinessProfileRequest(
            AgentToolArgHelpers.Str(args, "name") ?? current.Name,
            AgentToolArgHelpers.Str(args, "businessType") ?? current.BusinessType,
            AgentToolArgHelpers.Str(args, "email") ?? current.Email,
            AgentToolArgHelpers.Str(args, "phone") ?? current.Phone,
            AgentToolArgHelpers.Str(args, "address") ?? current.Address,
            AgentToolArgHelpers.Str(args, "website") ?? current.Website,
            AgentToolArgHelpers.Str(args, "description") ?? current.Description), cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = $"Company profile updated for \"{updated.Name}\".",
            ActionResult = new AiActionResultDto
            {
                Action = "UpdateCompanyProfile",
                Success = true,
                Message = $"Company profile updated for \"{updated.Name}\"."
            }
        };
    }
}

public sealed class UpdateTaxDefaultsTool : AiToolBase
{
    private readonly ISettingsService _settings;

    public UpdateTaxDefaultsTool(ISettingsService settings) => _settings = settings;

    public override AiToolName ToolName => AiToolName.UpdateTaxDefaults;
    public override string Description => "Update tax rate and related defaults.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.SettingsManage];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.UpdateTaxDefaults);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "update tax", "tax rate", "set tax", "currency");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var current = await _settings.GetSettingsAsync(cancellationToken);
        var updated = await _settings.UpdateSettingsAsync(new UpdateTenantSettingsRequest(
            AgentToolArgHelpers.Str(args, "currency") ?? current.Currency,
            current.Language,
            AgentToolArgHelpers.Dec(args, "taxRate") ?? current.TaxRate,
            AgentToolArgHelpers.Str(args, "invoicePrefix") ?? current.InvoicePrefix,
            current.EmailFromAddress,
            current.Theme,
            current.LogoUrl,
            current.Timezone,
            current.AiAssistantEnabled,
            current.AiShowSuggestions,
            current.EmailNotificationsEnabled,
            current.SystemNotificationsEnabled,
            current.OrderNotificationsEnabled,
            current.InventoryAlertsEnabled,
            current.PaymentAlertsEnabled,
            current.TaskNotificationsEnabled,
            current.InvoiceNotificationsEnabled,
            current.CustomerNotificationsEnabled,
            current.ProjectNotificationsEnabled), cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = $"Tax defaults updated. Tax rate: {updated.TaxRate}%, Currency: {updated.Currency}.",
            ActionResult = new AiActionResultDto
            {
                Action = "UpdateTaxDefaults",
                Success = true,
                Message = $"Tax defaults updated. Tax rate: {updated.TaxRate}%."
            }
        };
    }
}

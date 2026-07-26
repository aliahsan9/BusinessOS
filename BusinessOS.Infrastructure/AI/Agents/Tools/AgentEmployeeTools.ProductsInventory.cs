using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Inventory.Commands.AdjustStock;
using BusinessOS.Application.Features.Inventory.Commands.IncreaseStock;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Application.Features.Products.Commands.DeleteProduct;
using BusinessOS.Application.Features.Products.Commands.UpdateProduct;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using BusinessOS.Infrastructure.AI.Copilot.Tools;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents.Tools;

public sealed class SearchProductTool : AiToolBase
{
    private readonly IApplicationDbContext _db;
    public SearchProductTool(IApplicationDbContext db) => _db = db;

    public override AiToolName ToolName => AiToolName.SearchProduct;
    public override string Description => "Search products by name or SKU.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.ProductView];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.SearchProduct);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "search product", "find product", "پروڈکٹ تلاش");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var query = AgentToolArgHelpers.Str(args, "query")
            ?? AgentToolArgHelpers.Str(args, "sku")
            ?? context.Message;
        var q = query.Trim().ToLowerInvariant();

        var matches = await _db.Products.AsNoTracking()
            .Where(p => p.Name.ToLower().Contains(q) || p.SKU.ToLower().Contains(q))
            .OrderBy(p => p.Name)
            .Take(10)
            .Select(p => new { p.Id, p.Name, p.SKU, p.SalePrice })
            .ToListAsync(cancellationToken);

        if (matches.Count == 1 && context.ExecutionState is not null)
        {
            context.ExecutionState.ProductId = matches[0].Id;
            context.ExecutionState.ProductName = matches[0].Name;
        }

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = matches.Count > 0,
            Summary = matches.Count == 0
                ? $"No products found for \"{query}\"."
                : $"Found {matches.Count} product(s): " + string.Join("; ", matches.Select(m => $"{m.Name} [{m.SKU}]")),
            Data = matches,
            ActionResult = matches.Count == 1
                ? new AiActionResultDto
                {
                    Action = "SearchProduct",
                    Success = true,
                    Message = $"Found product \"{matches[0].Name}\".",
                    EntityType = "Product",
                    EntityId = matches[0].Id,
                    Route = $"/products/{matches[0].Id}"
                }
                : null
        };
    }
}

public sealed class CreateProductTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<CreateProductTool> _logger;

    public CreateProductTool(IMediator mediator, IApplicationDbContext db, ILogger<CreateProductTool> logger)
    {
        _mediator = mediator;
        _db = db;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.CreateProduct;
    public override string Description => "Create a product.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.ProductCreate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.CreateProduct);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate && ContainsAny(message, "product", "پروڈکٹ");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        try
        {
            var name = AgentToolArgHelpers.Str(args, "name");
            if (string.IsNullOrWhiteSpace(name))
                return Fail("Product name is required.");

            var categoryId = AgentToolArgHelpers.GuidVal(args, "categoryId")
                ?? context.ExecutionState?.CategoryId
                ?? await _db.Categories.AsNoTracking().Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);

            if (categoryId is null)
                return Fail("No product category found. Create a category first.");

            var sku = AgentToolArgHelpers.Str(args, "sku") ?? $"SKU-{Guid.NewGuid().ToString("N")[..8]}".ToUpperInvariant();
            var cost = AgentToolArgHelpers.Dec(args, "costPrice") ?? 0;
            var sale = AgentToolArgHelpers.Dec(args, "salePrice") ?? cost;
            var reorder = AgentToolArgHelpers.Int(args, "reorderLevel") ?? 10;

            var id = await _mediator.Send(new CreateProductCommand(
                categoryId.Value,
                name,
                sku,
                AgentToolArgHelpers.Str(args, "description"),
                cost,
                sale,
                reorder), cancellationToken);

            if (context.ExecutionState is not null)
            {
                context.ExecutionState.ProductId = id;
                context.ExecutionState.ProductName = name;
            }

            return Ok($"Product \"{name}\" has been created successfully.", id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreateProduct failed");
            return Fail(ex.Message);
        }
    }

    private AiToolResult Fail(string m) => new() { ToolName = ToolName.ToString(), Success = false, Summary = m };
    private AiToolResult Ok(string m, Guid id) => new()
    {
        ToolName = ToolName.ToString(),
        Success = true,
        Summary = m,
        ActionResult = new AiActionResultDto
        {
            Action = "CreateProduct",
            Success = true,
            Message = m,
            EntityType = "Product",
            EntityId = id,
            Route = $"/products/{id}"
        }
    };
}

public sealed class UpdateProductTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public UpdateProductTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.UpdateProduct;
    public override string Description => "Update a product.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.ProductUpdate];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.UpdateProduct);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "update product", "edit product", "پروڈکٹ اپڈیٹ");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var id = await ResolveProductIdAsync(args, context, cancellationToken);
        if (id is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Product not found." };

        var existing = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == id, cancellationToken);
        await _mediator.Send(new UpdateProductCommand(
            id.Value,
            existing.CategoryId,
            AgentToolArgHelpers.Str(args, "name") ?? existing.Name,
            AgentToolArgHelpers.Str(args, "sku") ?? existing.SKU,
            AgentToolArgHelpers.Str(args, "description") ?? existing.Description,
            AgentToolArgHelpers.Dec(args, "costPrice") ?? existing.CostPrice,
            AgentToolArgHelpers.Dec(args, "salePrice") ?? existing.SalePrice,
            AgentToolArgHelpers.Dec(args, "reorderLevel") ?? existing.ReorderLevel,
            AgentToolArgHelpers.Bool(args, "isActive") ?? existing.IsActive), cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = $"Product \"{existing.Name}\" was updated.",
            ActionResult = new AiActionResultDto
            {
                Action = "UpdateProduct",
                Success = true,
                Message = $"Product \"{existing.Name}\" was updated.",
                EntityType = "Product",
                EntityId = id,
                Route = $"/products/{id}"
            }
        };
    }

    private async Task<Guid?> ResolveProductIdAsync(JsonElement args, AiCopilotExecutionContext context, CancellationToken ct)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "productId") ?? context.ExecutionState?.ProductId;
        if (id is not null) return id;
        var name = AgentToolArgHelpers.Str(args, "name") ?? AgentToolArgHelpers.Str(args, "sku");
        if (string.IsNullOrWhiteSpace(name)) return null;
        var q = name.ToLowerInvariant();
        return await _db.Products.AsNoTracking()
            .Where(p => p.Name.ToLower().Contains(q) || p.SKU.ToLower().Contains(q))
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
    }
}

public sealed class DeleteProductTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public DeleteProductTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.DeleteProduct;
    public override string Description => "Delete a product.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.ProductDelete];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.DeleteProduct);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "delete product", "remove product", "پروڈکٹ حذف");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "productId") ?? context.ExecutionState?.ProductId;
        if (id is null)
        {
            var name = AgentToolArgHelpers.Str(args, "name") ?? AgentToolArgHelpers.Str(args, "sku");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var q = name.ToLowerInvariant();
                id = await _db.Products.AsNoTracking()
                    .Where(p => p.Name.ToLower().Contains(q) || p.SKU.ToLower().Contains(q))
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (id is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Product not found." };

        await _mediator.Send(new DeleteProductCommand(id.Value), cancellationToken);
        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = "Product deleted successfully.",
            ActionResult = new AiActionResultDto
            {
                Action = "DeleteProduct",
                Success = true,
                Message = "Product deleted successfully.",
                EntityType = "Product",
                EntityId = id
            }
        };
    }
}

public sealed class AdjustInventoryTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public AdjustInventoryTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.AdjustInventory;
    public override string Description => "Adjust inventory stock levels.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.InventoryAdjust];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.AdjustInventory);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "adjust inventory", "adjust stock", "اسٹاک ایڈجسٹ");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var productId = await ResolveProductAsync(args, context, cancellationToken);
        var qty = AgentToolArgHelpers.Dec(args, "quantity");
        if (productId is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Product not found." };
        if (qty is null or 0)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Quantity is required." };

        var type = AgentToolArgHelpers.Str(args, "transactionType") ?? "Adjustment";
        await _mediator.Send(new AdjustStockCommand(
            productId.Value,
            qty.Value,
            type,
            AgentToolArgHelpers.Str(args, "referenceNumber"),
            AgentToolArgHelpers.Str(args, "notes")), cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = $"Inventory adjusted by {qty} for product {productId}.",
            ActionResult = new AiActionResultDto
            {
                Action = "AdjustInventory",
                Success = true,
                Message = $"Inventory adjusted by {qty}.",
                EntityType = "Product",
                EntityId = productId,
                Route = $"/inventory"
            }
        };
    }

    private async Task<Guid?> ResolveProductAsync(JsonElement args, AiCopilotExecutionContext context, CancellationToken ct)
    {
        var id = AgentToolArgHelpers.GuidVal(args, "productId") ?? context.ExecutionState?.ProductId;
        if (id is not null) return id;
        var name = AgentToolArgHelpers.Str(args, "productName") ?? AgentToolArgHelpers.Str(args, "sku");
        if (string.IsNullOrWhiteSpace(name)) return null;
        var q = name.ToLowerInvariant();
        return await _db.Products.AsNoTracking()
            .Where(p => p.Name.ToLower().Contains(q) || p.SKU.ToLower().Contains(q))
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
    }
}

public sealed class ReceiveStockTool : AiToolBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _db;

    public ReceiveStockTool(IMediator mediator, IApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override AiToolName ToolName => AiToolName.ReceiveStock;
    public override string Description => "Receive stock into inventory.";
    public override IReadOnlyList<string> RequiredPermissions => [PermissionCodes.InventoryAdjust];
    public override string? ParameterSchemaJson => AgentToolSchemas.For(AiToolName.ReceiveStock);

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "receive stock", "receive inventory", "اسٹاک وصول");

    public override Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default) =>
        ExecuteWithArgsAsync(context, context.ToolArgs ?? JsonDocument.Parse("{}").RootElement, cancellationToken);

    public override async Task<AiToolResult> ExecuteWithArgsAsync(
        AiCopilotExecutionContext context, JsonElement args, CancellationToken cancellationToken = default)
    {
        var productId = AgentToolArgHelpers.GuidVal(args, "productId") ?? context.ExecutionState?.ProductId;
        if (productId is null)
        {
            var name = AgentToolArgHelpers.Str(args, "productName") ?? AgentToolArgHelpers.Str(args, "sku");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var q = name.ToLowerInvariant();
                productId = await _db.Products.AsNoTracking()
                    .Where(p => p.Name.ToLower().Contains(q) || p.SKU.ToLower().Contains(q))
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        var qty = AgentToolArgHelpers.Dec(args, "quantity");
        if (productId is null)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Product not found." };
        if (qty is null or <= 0)
            return new AiToolResult { ToolName = ToolName.ToString(), Success = false, Summary = "Quantity must be greater than zero." };

        await _mediator.Send(new IncreaseStockCommand(
            productId.Value,
            qty.Value,
            AgentToolArgHelpers.Str(args, "referenceNumber"),
            AgentToolArgHelpers.Str(args, "notes") ?? "Received via Sophia"), cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Success = true,
            Summary = $"Received {qty} units into inventory.",
            ActionResult = new AiActionResultDto
            {
                Action = "ReceiveStock",
                Success = true,
                Message = $"Received {qty} units into inventory.",
                EntityType = "Product",
                EntityId = productId,
                Route = "/inventory"
            }
        };
    }
}

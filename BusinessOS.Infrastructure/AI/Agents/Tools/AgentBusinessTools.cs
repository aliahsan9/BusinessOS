using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Onboarding.DTOs;
using BusinessOS.Application.Features.Onboarding.Services;
using BusinessOS.Application.Features.PurchaseOrders.Commands.CreatePurchaseOrder;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using BusinessOS.Application.Features.Reports.DTOs;
using BusinessOS.Application.Features.Reports.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Copilot.Tools;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents.Tools;

public sealed class GetInventorySummaryTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetInventorySummaryTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetInventorySummary;
    public override string Description => "Aggregate inventory and product stock counts with estimated stock value.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionRead or AiCopilotIntent.ReportGeneration or AiCopilotIntent.Recommendation or AiCopilotIntent.Workflow
        && ContainsAny(message, "inventory", "stock", "warehouse", "sku");

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var productCount = await _context.Products.CountAsync(p => p.IsActive, cancellationToken);
        var inventoryRows = await _context.Inventories
            .Include(i => i.Product)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var totalUnits = inventoryRows.Sum(i => i.CurrentStock);
        var lowStock = inventoryRows.Count(i => i.CurrentStock > 0 && i.CurrentStock <= i.ReorderLevel);
        var outOfStock = inventoryRows.Count(i => i.CurrentStock <= 0);
        var stockValue = inventoryRows.Sum(i => i.CurrentStock * (i.Product?.CostPrice ?? 0m));

        var data = new
        {
            activeProducts = productCount,
            inventorySkus = inventoryRows.Count,
            totalUnits,
            lowStockCount = lowStock,
            outOfStockCount = outOfStock,
            estimatedStockValue = Math.Round(stockValue, 2)
        };

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = data,
            Summary =
                $"Inventory: {inventoryRows.Count:N0} SKU(s), {totalUnits:N0} units on hand, " +
                $"estimated value {stockValue:C}. Low stock: {lowStock}, out of stock: {outOfStock}."
        };
    }
}

public sealed class GetLowStockTool : AiToolBase
{
    private readonly IInventoryService _inventoryService;

    public GetLowStockTool(IInventoryService inventoryService) => _inventoryService = inventoryService;

    public override AiToolName ToolName => AiToolName.GetLowStock;
    public override string Description => "List products at or below reorder level.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "low stock", "reorder", "running low", "out of stock", "stock alert")
        || intent is AiCopilotIntent.Recommendation or AiCopilotIntent.ReportGeneration;

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var items = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        var lines = items.Take(15)
            .Select(i => $"{i.ProductName} ({i.ProductSku}): {i.CurrentStock:N0} / reorder {i.ReorderLevel:N0}");

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = items,
            Summary = items.Count == 0
                ? "No low-stock products right now."
                : $"Low stock ({items.Count}):\n{string.Join("\n", lines)}"
        };
    }
}

public sealed class GetDeadStockTool : AiToolBase
{
    private readonly IApplicationDbContext _context;

    public GetDeadStockTool(IApplicationDbContext context) => _context = context;

    public override AiToolName ToolName => AiToolName.GetDeadStock;
    public override string Description => "Find products with stock but no sales in the last 90 days.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "dead stock", "slow moving", "no sales", "unsold", "stale stock")
        || (intent is AiCopilotIntent.Recommendation && ContainsAny(message, "dead", "slow"));

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-90);

        var soldProductIds = await _context.OrderItems
            .Where(oi => oi.Order.OrderDate >= since && !oi.Order.IsDeleted)
            .Select(oi => oi.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var dead = await _context.Inventories
            .AsNoTracking()
            .Include(i => i.Product)
            .Where(i => i.CurrentStock > 0 && i.Product.IsActive && !soldProductIds.Contains(i.ProductId))
            .OrderByDescending(i => i.CurrentStock)
            .Take(25)
            .Select(i => new
            {
                i.ProductId,
                ProductName = i.Product.Name,
                Sku = i.Product.SKU,
                i.CurrentStock,
                EstimatedValue = i.CurrentStock * i.Product.CostPrice
            })
            .ToListAsync(cancellationToken);

        var lines = dead.Select(d => $"{d.ProductName} ({d.Sku}): {d.CurrentStock:N0} units, ~{d.EstimatedValue:C}");

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = dead,
            Summary = dead.Count == 0
                ? "No dead stock detected (all stocked products had sales in the last 90 days)."
                : $"Dead stock candidates ({dead.Count}) with no sales in 90 days:\n{string.Join("\n", lines)}"
        };
    }
}

public sealed class CreatePurchaseOrderDraftTool : AiToolBase
{
    private readonly IInventoryService _inventoryService;
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly ILogger<CreatePurchaseOrderDraftTool> _logger;

    public CreatePurchaseOrderDraftTool(
        IInventoryService inventoryService,
        IApplicationDbContext context,
        ISender sender,
        ILogger<CreatePurchaseOrderDraftTool> logger)
    {
        _inventoryService = inventoryService;
        _context = context;
        _sender = sender;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.CreatePurchaseOrderDraft;
    public override string Description => "Create a draft purchase order for low-stock items using an active supplier.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ActionCreate or AiCopilotIntent.Workflow or AiCopilotIntent.Recommendation
        && ContainsAny(message, "purchase order", "create po", "draft po", "reorder", "buy stock");

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var lowStock = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        if (lowStock.Count == 0)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = false,
                Summary = "No low-stock products to include in a purchase order draft."
            };
        }

        var supplier = await _context.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (supplier is null)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = false,
                Summary = "No active supplier found. Add a supplier before creating a purchase order draft."
            };
        }

        var productIds = lowStock.Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var lines = new List<CreatePurchaseOrderLineItemDto>();
        foreach (var item in lowStock.Take(20))
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                continue;

            var qty = item.SuggestedReorderQuantity > 0
                ? item.SuggestedReorderQuantity
                : Math.Max(item.ReorderLevel, 1);

            lines.Add(new CreatePurchaseOrderLineItemDto(item.ProductId, qty, product.CostPrice));
        }

        if (lines.Count == 0)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = false,
                Summary = "Could not build purchase order lines from low-stock products."
            };
        }

        try
        {
            var poId = await _sender.Send(
                new CreatePurchaseOrderCommand(
                    supplier.Id,
                    DateTime.UtcNow,
                    PurchaseOrderStatusNames.Draft,
                    $"AI-DRAFT-{DateTime.UtcNow:yyyyMMddHHmm}",
                    "Draft created by AI employee from low-stock recommendations.",
                    lines),
                cancellationToken);

            _logger.LogInformation("AI created purchase order draft {PurchaseOrderId}", poId);

            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = new { purchaseOrderId = poId, supplierId = supplier.Id, lineCount = lines.Count },
                Summary = $"Created draft purchase order {poId} for supplier '{supplier.Name}' with {lines.Count} line(s).",
                ActionResult = new AiActionResultDto
                {
                    Action = "CreatePurchaseOrderDraft",
                    Success = true,
                    Message = $"Draft PO created for {supplier.Name}.",
                    EntityType = "PurchaseOrder",
                    EntityId = poId,
                    Route = $"/purchase-orders/{poId}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create purchase order draft");
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = false,
                Summary = $"Could not create purchase order draft: {ex.Message}"
            };
        }
    }
}

public sealed class GenerateInventoryReportTool : AiToolBase
{
    private readonly IApplicationDbContext _context;
    private readonly IReportService _reportService;
    private readonly ILogger<GenerateInventoryReportTool> _logger;

    public GenerateInventoryReportTool(
        IApplicationDbContext context,
        IReportService reportService,
        ILogger<GenerateInventoryReportTool> logger)
    {
        _context = context;
        _reportService = reportService;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.GenerateInventoryReport;
    public override string Description => "Build an inventory summary report (structured + optional business summary PDF).";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ReportGeneration or AiCopilotIntent.Workflow
        || ContainsAny(message, "inventory report", "stock report", "warehouse report");

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var inventories = await _context.Inventories
            .AsNoTracking()
            .Include(i => i.Product)
            .ToListAsync(cancellationToken);

        var summary = new
        {
            skuCount = inventories.Count,
            totalUnits = inventories.Sum(i => i.CurrentStock),
            lowStock = inventories.Count(i => i.CurrentStock > 0 && i.CurrentStock <= i.ReorderLevel),
            outOfStock = inventories.Count(i => i.CurrentStock <= 0),
            estimatedValue = Math.Round(inventories.Sum(i => i.CurrentStock * (i.Product?.CostPrice ?? 0m)), 2),
            topByValue = inventories
                .OrderByDescending(i => i.CurrentStock * (i.Product?.CostPrice ?? 0m))
                .Take(10)
                .Select(i => new
                {
                    i.Product?.Name,
                    i.Product?.SKU,
                    i.CurrentStock,
                    Value = Math.Round(i.CurrentStock * (i.Product?.CostPrice ?? 0m), 2)
                })
                .ToList()
        };

        AiActionResultDto? action = null;
        try
        {
            var (start, end, _) = ResolveDateRange(context.Message);
            var pdf = await _reportService.GenerateBusinessSummaryAsync(
                new ReportQueryParams { StartDate = start, EndDate = end, Period = "month" },
                cancellationToken);

            action = new AiActionResultDto
            {
                Action = "DownloadReport",
                Success = true,
                Message = $"Business summary PDF ready: {pdf.FileName}",
                EntityType = "GeneratedReport",
                EntityId = pdf.HistoryId,
                Route = $"/reports/history/{pdf.HistoryId}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Optional business summary PDF skipped for inventory report");
            action = new AiActionResultDto
            {
                Action = "OpenReports",
                Success = true,
                Message = "Open Reports to download related PDFs.",
                Route = "/reports"
            };
        }

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = summary,
            Summary =
                $"Inventory report: {summary.skuCount:N0} SKUs, {summary.totalUnits:N0} units, " +
                $"value ~{summary.estimatedValue:C}. Low: {summary.lowStock}, out: {summary.outOfStock}.",
            ActionResult = action
        };
    }
}

public sealed class GenerateSalesReportTool : AiToolBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<GenerateSalesReportTool> _logger;

    public GenerateSalesReportTool(IReportService reportService, ILogger<GenerateSalesReportTool> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.GenerateSalesReport;
    public override string Description => "Generate a revenue/sales PDF report for a date range.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.ReportGeneration
        || ContainsAny(message, "sales report", "revenue report");

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var (start, end, label) = ResolveDateRange(context.Message);

        try
        {
            var result = await _reportService.GenerateRevenueReportAsync(
                new ReportQueryParams { StartDate = start, EndDate = end, Period = label },
                cancellationToken);

            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Data = new { result.FileName, result.HistoryId, period = label },
                Summary = $"Sales/revenue report generated for {label}: {result.FileName}.",
                ActionResult = new AiActionResultDto
                {
                    Action = "DownloadReport",
                    Success = true,
                    Message = $"Download {result.FileName}",
                    EntityType = "GeneratedReport",
                    EntityId = result.HistoryId,
                    Route = $"/reports/history/{result.HistoryId}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GenerateSalesReport failed");
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = false,
                Summary = $"Could not generate sales report: {ex.Message}"
            };
        }
    }
}

public sealed class GetPurchaseRecommendationsTool : AiToolBase
{
    private readonly IInventoryService _inventoryService;

    public GetPurchaseRecommendationsTool(IInventoryService inventoryService) =>
        _inventoryService = inventoryService;

    public override AiToolName ToolName => AiToolName.GetPurchaseRecommendations;
    public override string Description => "Combine low-stock and reorder products into purchase recommendations.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.Recommendation or AiCopilotIntent.ReportGeneration or AiCopilotIntent.Workflow
        || ContainsAny(message, "what should i buy", "recommend", "reorder", "purchase suggestion");

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var low = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        var reorder = await _inventoryService.GetReorderProductsAsync(cancellationToken);

        var byProduct = new Dictionary<Guid, object>();
        foreach (var item in low.Concat(reorder))
        {
            byProduct[item.ProductId] = new
            {
                item.ProductId,
                item.ProductName,
                item.ProductSku,
                item.CurrentStock,
                item.ReorderLevel,
                item.SuggestedReorderQuantity,
                item.IsLowStock,
                item.IsOutOfStock
            };
        }

        var recommendations = byProduct.Values.ToList();
        var lines = low.Concat(reorder)
            .GroupBy(x => x.ProductId)
            .Select(g => g.First())
            .Take(15)
            .Select(i =>
                $"{i.ProductName}: order ~{i.SuggestedReorderQuantity:N0} (have {i.CurrentStock:N0}, reorder at {i.ReorderLevel:N0})");

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = recommendations,
            Summary = recommendations.Count == 0
                ? "No purchase recommendations right now — stock looks healthy."
                : $"Purchase recommendations ({recommendations.Count}):\n{string.Join("\n", lines)}"
        };
    }
}

public sealed class ApplyOnboardingProfileTool : AiToolBase
{
    private readonly IOnboardingService _onboardingService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ApplyOnboardingProfileTool> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApplyOnboardingProfileTool(
        IOnboardingService onboardingService,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<ApplyOnboardingProfileTool> logger)
    {
        _onboardingService = onboardingService;
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public override AiToolName ToolName => AiToolName.ApplyOnboardingProfile;
    public override string Description => "Apply onboarding business profile from message or conversation memory.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        intent is AiCopilotIntent.Onboarding
        || ContainsAny(message, "apply onboarding", "save business profile", "finish setup");

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(context.Memory.OnboardingDataJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string?>>(
                    context.Memory.OnboardingDataJson, JsonOptions);
                if (parsed is not null)
                {
                    foreach (var kv in parsed)
                        data[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not parse OnboardingDataJson");
            }
        }

        ParseLooseMessage(context.Message, data);

        var name = data.GetValueOrDefault("business_name")
            ?? data.GetValueOrDefault("name")
            ?? "My Business";
        var industry = data.GetValueOrDefault("industry") ?? "General";
        var currency = data.GetValueOrDefault("currency") ?? "USD";
        var timezone = data.GetValueOrDefault("timezone")
            ?? data.GetValueOrDefault("country_timezone")
            ?? "UTC";

        await _onboardingService.SaveBusinessProfileAsync(
            new SaveOnboardingBusinessProfileRequest(
                name.Trim(),
                null,
                null,
                industry.Trim(),
                data.GetValueOrDefault("size") is { } size ? $"Business size: {size}" : null,
                currency.Trim(),
                timezone.Trim()),
            cancellationToken);

        if (_currentUser.TenantId is Guid tenantId
            && decimal.TryParse(data.GetValueOrDefault("tax"), out var tax))
        {
            var settings = await _context.TenantSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
            if (settings is not null)
            {
                settings.TaxRate = Math.Clamp(tax, 0, 100);
                settings.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        await _onboardingService.CompleteAsync(cancellationToken);

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = data,
            Summary = $"Applied onboarding profile for '{name}' ({industry}, {currency}).",
            ActionResult = new AiActionResultDto
            {
                Action = "ApplyOnboardingProfile",
                Success = true,
                Message = "Business profile saved.",
                Route = "/settings"
            }
        };
    }

    private static void ParseLooseMessage(string message, Dictionary<string, string?> data)
    {
        // Lightweight extraction for free-form messages like "Business Acme, industry retail, currency PKR"
        var parts = message.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower.StartsWith("business") || lower.StartsWith("name"))
                data["business_name"] = part[(part.IndexOf(' ') + 1)..].Trim();
            else if (lower.Contains("industry"))
                data["industry"] = part[(part.IndexOf(' ') + 1)..].Trim();
            else if (lower.Contains("currency"))
                data["currency"] = part[(part.IndexOf(' ') + 1)..].Trim().ToUpperInvariant();
            else if (lower.Contains("timezone") || lower.Contains("time zone"))
                data["timezone"] = part[(part.IndexOf(' ') + 1)..].Trim();
        }
    }
}

public sealed class GetBusinessSettingsTool : AiToolBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetBusinessSettingsTool(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public override AiToolName ToolName => AiToolName.GetBusinessSettings;
    public override string Description => "Read current tenant business settings.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "settings", "tax rate", "currency", "timezone", "business settings", "tenant settings")
        || intent is AiCopilotIntent.Onboarding;

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId is null)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = false,
                Summary = "Tenant context is required to read settings."
            };
        }

        var settings = await _context.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        var tenant = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Name, t.BusinessType, t.Website })
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null && tenant is null)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = false,
                Summary = "No business settings found."
            };
        }

        var data = new
        {
            businessName = tenant?.Name,
            industry = tenant?.BusinessType,
            website = tenant?.Website,
            currency = settings?.Currency ?? "USD",
            timezone = settings?.Timezone ?? "UTC",
            language = settings?.Language ?? "en",
            taxRate = settings?.TaxRate ?? 0,
            aiAssistantEnabled = settings?.AiAssistantEnabled ?? true
        };

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = data,
            Summary =
                $"Business '{data.businessName}' · {data.industry} · currency {data.currency}, " +
                $"timezone {data.timezone}, tax {data.taxRate}%."
        };
    }
}

public sealed class GetNotificationsSummaryTool : AiToolBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationsSummaryTool(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public override AiToolName ToolName => AiToolName.GetNotificationsSummary;
    public override string Description => "Summarize recent unread notifications for the current user.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "notification", "alerts", "unread", "inbox");

    public override async Task<AiToolResult> ExecuteAsync(
        AiCopilotExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return new AiToolResult
            {
                ToolName = ToolName.ToString(),
                Success = false,
                Summary = "User context is required."
            };
        }

        var unread = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .Select(n => new { n.Id, n.Title, n.Message, n.Type, n.CreatedAt, n.Link })
            .ToListAsync(cancellationToken);

        var lines = unread.Select(n => $"• [{n.Type}] {n.Title}: {n.Message}");

        return new AiToolResult
        {
            ToolName = ToolName.ToString(),
            Data = unread,
            Summary = unread.Count == 0
                ? "You have no unread notifications."
                : $"Unread notifications ({unread.Count}):\n{string.Join("\n", lines)}"
        };
    }
}

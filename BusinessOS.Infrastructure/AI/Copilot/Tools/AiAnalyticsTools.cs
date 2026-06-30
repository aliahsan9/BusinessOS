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

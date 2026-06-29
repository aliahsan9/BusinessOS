using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.AI;

public sealed class AiRetrievalService : IAiRetrievalService
{
    private readonly IAiContextService _contextService;

    public AiRetrievalService(IAiContextService contextService)
    {
        _contextService = contextService;
    }

    public AiRetrievalScope DetermineScope(string message, AiPageContextDto page)
    {
        var text = message.ToLowerInvariant();

        if (ContainsAny(text, "overdue", "unpaid", "outstanding", "past due"))
            return AiRetrievalScope.OverdueInvoices;

        if (ContainsAny(text, "highest revenue", "top customer", "most revenue", "best customer", "revenue ranking"))
            return AiRetrievalScope.RevenueRanking;

        if (ContainsAny(text, "project progress", "delayed task", "team workload", "delayed tasks", "behind schedule"))
            return AiRetrievalScope.ProjectProgress;

        if (ContainsAny(text, "this customer", "current customer", "summarize this customer", "customer revenue", "about this customer"))
            return page.CustomerId is not null
                ? AiRetrievalScope.CustomerBundle
                : AiRetrievalScope.PageDefaults;

        if (ContainsAny(text, "customer") && page.CustomerId is not null)
            return AiRetrievalScope.CustomerBundle;

        if (ContainsAny(text, "this invoice", "current invoice") && page.InvoiceId is not null)
            return AiRetrievalScope.CurrentInvoice;

        if (ContainsAny(text, "this order", "this project", "current project") && page.OrderId is not null)
            return AiRetrievalScope.CurrentOrder;

        if (page.CustomerId is not null && ContainsAny(text, "customer", "invoice", "order", "payment", "revenue"))
            return AiRetrievalScope.CustomerBundle;

        if (page.Module is "invoices" && ContainsAny(text, "invoice", "payment", "revenue", "month"))
            return AiRetrievalScope.OverdueInvoices;

        if (page.Module is "orders" && ContainsAny(text, "project", "task", "progress", "delay"))
            return AiRetrievalScope.ProjectProgress;

        if (page.Module is "customers")
            return AiRetrievalScope.PageDefaults;

        if (page.Module is "invoices")
            return AiRetrievalScope.OverdueInvoices;

        if (page.Module is "orders")
            return AiRetrievalScope.ProjectProgress;

        return AiRetrievalScope.PageDefaults;
    }

    public async Task<AiContextDto> RetrieveAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = _contextService.BuildPageContext(request);
        var scope = DetermineScope(request.Message.Trim(), page);
        return await _contextService.BuildContextAsync(request, scope, cancellationToken);
    }

    public AiRetrievedSourcesDto BuildSources(AiContextDto context) =>
        new()
        {
            Customers = context.Customer is not null ? 1 : 0,
            Orders = context.Orders.Count,
            Invoices = context.Invoices.Count,
            Projects = context.Projects.Count
        };

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}

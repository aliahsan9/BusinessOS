using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Invoices.Queries;
using BusinessOS.Application.Features.Invoices.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Invoices.Queries.GetAllInvoices;

public sealed class GetAllInvoicesQueryHandler
    : IRequestHandler<GetAllInvoicesQuery, PagedResult<InvoiceSummaryResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<InvoiceSummaryResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["invoicedate"] = x => x.InvoiceDate,
            ["duedate"] = x => x.DueDate,
            ["createdat"] = x => x.CreatedAt,
            ["status"] = x => x.Status,
            ["grandtotal"] = x => x.GrandTotal,
            ["invoicenumber"] = x => x.InvoiceNumber,
            ["customername"] = x => x.CustomerName
        };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllInvoicesQueryHandler> _logger;

    public GetAllInvoicesQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllInvoicesQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<InvoiceSummaryResponse>> Handle(
        GetAllInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);

        var query = _context.Invoices
            .AsNoTracking()
            .Select(InvoiceProjections.ToSummary);

        if (request.CustomerId.HasValue)
            query = query.Where(x => x.CustomerId == request.CustomerId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.InvoiceNumber.Contains(search) ||
                x.OrderNumber.Contains(search) ||
                x.CustomerName.Contains(search) ||
                x.Status.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(
                request.SortBy,
                request.SortDirection,
                SortFields,
                x => x.InvoiceDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var orderIds = items.Select(x => x.OrderId).Distinct().ToList();
        var amountPaidByOrderId = await InvoicePaymentCalculator.GetAmountPaidByOrderIdsAsync(
            _context,
            orderIds,
            cancellationToken);

        foreach (var item in items)
            InvoicePaymentCalculator.ApplyPaymentAmounts(item, amountPaidByOrderId);

        _logger.LogInformation(
            "Retrieved {Count} invoices (page {Page}, total {Total})",
            items.Count,
            page,
            totalCount);

        return new PagedResult<InvoiceSummaryResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

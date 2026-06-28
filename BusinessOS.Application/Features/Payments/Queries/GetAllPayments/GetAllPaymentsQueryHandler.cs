using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Payments.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Payments.Queries.GetAllPayments;

public sealed class GetAllPaymentsQueryHandler
    : IRequestHandler<GetAllPaymentsQuery, PagedResult<PaymentSummaryResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<PaymentSummaryResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["paymentdate"] = x => x.PaymentDate,
            ["createdat"] = x => x.CreatedAt,
            ["amount"] = x => x.Amount,
            ["paymentmethod"] = x => x.PaymentMethod,
            ["customername"] = x => x.CustomerName,
            ["ordernumber"] = x => x.OrderNumber
        };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllPaymentsQueryHandler> _logger;

    public GetAllPaymentsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllPaymentsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<PaymentSummaryResponse>> Handle(
        GetAllPaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);

        var query = _context.Payments
            .AsNoTracking()
            .Select(PaymentProjections.ToSummary);

        if (request.CustomerId.HasValue)
            query = query.Where(x => x.CustomerId == request.CustomerId.Value);

        if (request.OrderId.HasValue)
            query = query.Where(x => x.OrderId == request.OrderId.Value);

        if (!string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            var method = request.PaymentMethod.Trim();
            query = query.Where(x => x.PaymentMethod == method);
        }

        if (request.DateFrom.HasValue)
        {
            var dateFrom = request.DateFrom.Value.ToUniversalTime();
            query = query.Where(x => x.PaymentDate >= dateFrom);
        }

        if (request.DateTo.HasValue)
        {
            var dateTo = request.DateTo.Value.ToUniversalTime();
            query = query.Where(x => x.PaymentDate <= dateTo);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(
                request.SortBy,
                request.SortDirection,
                SortFields,
                x => x.PaymentDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} payments (page {Page}, total {Total})",
            items.Count,
            page,
            totalCount);

        return new PagedResult<PaymentSummaryResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

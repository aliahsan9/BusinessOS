using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Orders.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Orders.Queries.GetAllOrders;

public sealed class GetAllOrdersQueryHandler
    : IRequestHandler<GetAllOrdersQuery, PagedResult<OrderSummaryDto>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<OrderSummaryDto, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ordernumber"] = x => x.OrderNumber,
            ["orderdate"] = x => x.OrderDate,
            ["createdat"] = x => x.CreatedAt,
            ["status"] = x => x.Status,
            ["grandtotal"] = x => x.GrandTotal,
            ["customername"] = x => x.CustomerName
        };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllOrdersQueryHandler> _logger;

    public GetAllOrdersQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllOrdersQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<OrderSummaryDto>> Handle(
        GetAllOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);

        var query = _context.Orders
            .AsNoTracking()
            .Select(OrderProjections.ToSummaryDto);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.OrderNumber.Contains(search) ||
                x.CustomerName.Contains(search) ||
                x.CustomerEmail.Contains(search) ||
                x.Status.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(
                request.SortBy,
                request.SortDirection,
                SortFields,
                x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} orders (page {Page}, total {Total})",
            items.Count,
            page,
            totalCount);

        return new PagedResult<OrderSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

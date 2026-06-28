using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Customers.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Customers.Queries.GetCustomerOrders;

public sealed class GetCustomerOrdersQueryHandler
    : IRequestHandler<GetCustomerOrdersQuery, PagedResult<CustomerOrderResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<CustomerOrderResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ordernumber"] = x => x.OrderNumber,
            ["orderdate"] = x => x.OrderDate,
            ["status"] = x => x.Status,
            ["grandtotal"] = x => x.GrandTotal,
            ["createdat"] = x => x.CreatedAt
        };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetCustomerOrdersQueryHandler> _logger;

    public GetCustomerOrdersQueryHandler(
        IApplicationDbContext context,
        ILogger<GetCustomerOrdersQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<CustomerOrderResponse>> Handle(
        GetCustomerOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var customerExists = await _context.Customers
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (!customerExists)
            throw new NotFoundException("Customer not found");

        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);

        var query = _context.Orders
            .AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId)
            .Select(CustomerProjections.ToOrderResponse);

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
            "Retrieved {Count} orders for customer {CustomerId}",
            items.Count,
            request.CustomerId);

        return new PagedResult<CustomerOrderResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

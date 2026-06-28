using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Customers.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Customers.Queries.GetCustomerOrders;

public record GetCustomerOrdersQuery(
    Guid CustomerId,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<CustomerOrderResponse>>;

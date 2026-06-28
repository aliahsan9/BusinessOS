using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Orders.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Orders.Queries.GetAllOrders;

public sealed record GetAllOrdersQuery(
    string? Status = null,
    string? Search = null,
    int Page = PaginationParams.DefaultPage,
    int PageSize = PaginationParams.DefaultPageSize,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc) : IRequest<PagedResult<OrderSummaryDto>>;

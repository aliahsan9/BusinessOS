using BusinessOS.Application.Common.Models;
using MediatR;

namespace BusinessOS.Application.Features.PurchaseOrders.Queries.GetAllPurchaseOrders;

public record GetAllPurchaseOrdersQuery(
    Guid? SupplierId,
    string? Status,
    string? Search,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<Queries.PurchaseOrderSummaryResponse>>;

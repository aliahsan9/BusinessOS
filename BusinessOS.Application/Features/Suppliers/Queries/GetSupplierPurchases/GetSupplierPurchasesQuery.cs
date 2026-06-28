using BusinessOS.Application.Common.Models;
using MediatR;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetSupplierPurchases;

public record GetSupplierPurchasesQuery(
    Guid SupplierId,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<Queries.SupplierPurchaseSummaryResponse>>;

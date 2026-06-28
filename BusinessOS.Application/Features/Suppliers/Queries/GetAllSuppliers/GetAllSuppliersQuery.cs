using BusinessOS.Application.Common.Models;
using MediatR;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetAllSuppliers;

public record GetAllSuppliersQuery(
    string? Search,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<Queries.SupplierSummaryResponse>>;

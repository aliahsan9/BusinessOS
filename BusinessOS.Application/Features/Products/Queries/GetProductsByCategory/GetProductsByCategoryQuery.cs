using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Products.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Products.Queries.GetProductsByCategory;

public sealed record GetProductsByCategoryQuery(
    Guid CategoryId,
    string? Search = null,
    int Page = PaginationParams.DefaultPage,
    int PageSize = PaginationParams.DefaultPageSize,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc) : IRequest<PagedResult<ProductDto>>;

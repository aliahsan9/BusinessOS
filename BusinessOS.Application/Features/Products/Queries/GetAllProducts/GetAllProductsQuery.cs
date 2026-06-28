using BusinessOS.Application.Features.Products.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Products.Queries.GetAllProducts;

public sealed record GetAllProductsQuery(
    Guid? CategoryId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedProductsResult>;

public sealed class PagedProductsResult
{
    public IReadOnlyList<ProductDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Products.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Products.Queries.GetAllProducts;

public sealed class GetAllProductsQueryHandler
    : IRequestHandler<GetAllProductsQuery, PagedResult<ProductDto>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<ProductDto, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = x => x.Name,
            ["sku"] = x => x.SKU,
            ["saleprice"] = x => x.SalePrice,
            ["costprice"] = x => x.CostPrice,
            ["currentstock"] = x => x.CurrentStock,
            ["isactive"] = x => x.IsActive
        };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetAllProductsQueryHandler> _logger;

    public GetAllProductsQueryHandler(
        IApplicationDbContext context,
        ILogger<GetAllProductsQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<ProductDto>> Handle(
        GetAllProductsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);

        var query = _context.Products.AsNoTracking().Select(ProductProjections.ToDto);

        if (request.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == request.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.Name.Contains(search) ||
                x.SKU.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySort(
                request.SortBy,
                request.SortDirection,
                SortFields,
                x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} products (page {Page}, total {Total})",
            items.Count,
            page,
            totalCount);

        return new PagedResult<ProductDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

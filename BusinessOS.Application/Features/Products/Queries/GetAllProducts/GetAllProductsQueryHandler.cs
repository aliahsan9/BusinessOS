using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Products.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Products.Queries.GetAllProducts;

public sealed class GetAllProductsQueryHandler
    : IRequestHandler<GetAllProductsQuery, PagedProductsResult>
{
    private readonly IApplicationDbContext _context;

    public GetAllProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedProductsResult> Handle(
        GetAllProductsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _context.Products.AsNoTracking();

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
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductDto
            {
                Id = x.Id,
                CategoryId = x.CategoryId,
                Name = x.Name,
                SKU = x.SKU,
                Description = x.Description,
                CostPrice = x.CostPrice,
                SalePrice = x.SalePrice,
                CurrentStock = x.CurrentStock,
                ReorderLevel = x.ReorderLevel,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return new PagedProductsResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}

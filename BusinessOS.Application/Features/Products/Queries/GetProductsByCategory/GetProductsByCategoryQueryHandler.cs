using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Products.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Products.Queries.GetProductsByCategory;

public class GetProductsByCategoryQueryHandler
    : IRequestHandler<GetProductsByCategoryQuery, List<ProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductsByCategoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductDto>> Handle(
        GetProductsByCategoryQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(x => x.CategoryId == request.CategoryId)
            .OrderBy(x => x.Name)
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
    }
}

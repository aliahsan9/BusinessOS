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

    public async Task<List<ProductDto>> Handle(GetProductsByCategoryQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products
            .Where(x => x.CategoryId == request.CategoryId)
            .Select(x => new ProductDto
            {
                Id = x.Id,
                Name = x.Name,
                SKU = x.SKU,
                SalePrice = x.SalePrice
            })
            .ToListAsync(cancellationToken);
    }
}

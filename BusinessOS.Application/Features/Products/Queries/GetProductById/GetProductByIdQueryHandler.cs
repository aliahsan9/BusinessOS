using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Products.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Products.Queries.GetProductById;

public sealed class GetProductByIdQueryHandler
    : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly IApplicationDbContext _context;

    public GetProductByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<ProductDto?> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken) =>
        _context.Products
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
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
            .FirstOrDefaultAsync(cancellationToken);
}

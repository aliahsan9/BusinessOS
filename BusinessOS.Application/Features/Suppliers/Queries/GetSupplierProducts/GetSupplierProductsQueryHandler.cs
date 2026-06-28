using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Suppliers.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetSupplierProducts;

public sealed class GetSupplierProductsQueryHandler
    : IRequestHandler<GetSupplierProductsQuery, IReadOnlyList<SupplierProductSummaryResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetSupplierProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SupplierProductSummaryResponse>> Handle(
        GetSupplierProductsQuery request,
        CancellationToken cancellationToken)
    {
        var supplierExists = await _context.Suppliers
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.SupplierId, cancellationToken);

        if (!supplierExists)
            throw new NotFoundException("Supplier not found.");

        var products = await _context.PurchaseItems
            .AsNoTracking()
            .Where(x => x.Purchase.SupplierId == request.SupplierId)
            .GroupBy(x => new { x.ProductId, x.Product!.Name, x.Product.SKU })
            .Select(g => new SupplierProductSummaryResponse
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name,
                ProductSku = g.Key.SKU,
                LastPurchaseDate = g.Max(x => x.Purchase.PurchaseDate),
                TotalQuantityPurchased = g.Sum(x => x.Quantity)
            })
            .OrderBy(x => x.ProductName)
            .ToListAsync(cancellationToken);

        return products;
    }
}

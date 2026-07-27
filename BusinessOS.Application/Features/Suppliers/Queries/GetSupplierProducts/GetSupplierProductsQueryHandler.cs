using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Suppliers.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetSupplierProducts;

public sealed class GetSupplierProductsQueryHandler
    : IRequestHandler<GetSupplierProductsQuery, IReadOnlyList<SupplierProductSummaryResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;

    public GetSupplierProductsQueryHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
    }

    public async Task<IReadOnlyList<SupplierProductSummaryResponse>> Handle(
        GetSupplierProductsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.SupplierProducts(tenantId, request.SupplierId);

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var supplierExists = await _context.Suppliers
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == request.SupplierId, ct);

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
                    .ToListAsync(ct);

                return (IReadOnlyList<SupplierProductSummaryResponse>)products;
            },
            absoluteExpiration: _cacheSettings.DefaultExpiration,
            cancellationToken: cancellationToken);
    }
}

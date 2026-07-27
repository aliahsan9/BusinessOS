using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Inventory.Queries.GetLowStockProducts;

public sealed record GetLowStockProductsQuery : IRequest<IReadOnlyList<InventorySummaryResponse>>;

public sealed class GetLowStockProductsQueryHandler
    : IRequestHandler<GetLowStockProductsQuery, IReadOnlyList<InventorySummaryResponse>>
{
    private readonly IInventoryService _inventoryService;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;

    public GetLowStockProductsQueryHandler(
        IInventoryService inventoryService,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings)
    {
        _inventoryService = inventoryService;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
    }

    public async Task<IReadOnlyList<InventorySummaryResponse>> Handle(
        GetLowStockProductsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.InventoryLowStock(tenantId);

        return await _cache.GetOrSetAsync<List<InventorySummaryResponse>>(
            key,
            ct => _inventoryService.GetLowStockProductsAsync(ct),
            absoluteExpiration: _cacheSettings.DefaultExpiration,
            cancellationToken: cancellationToken);
    }
}

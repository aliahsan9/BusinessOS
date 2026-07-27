using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Inventory.Queries.GetInventoryByProductId;

public sealed record GetInventoryByProductIdQuery(Guid ProductId) : IRequest<InventoryResponse>;

public sealed class GetInventoryByProductIdQueryHandler
    : IRequestHandler<GetInventoryByProductIdQuery, InventoryResponse>
{
    private readonly IInventoryService _inventoryService;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;

    public GetInventoryByProductIdQueryHandler(
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

    public Task<InventoryResponse> Handle(
        GetInventoryByProductIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.InventoryByProduct(tenantId, request.ProductId);

        return _cache.GetOrSetAsync(
            key,
            ct => _inventoryService.GetByProductIdAsync(request.ProductId, ct),
            absoluteExpiration: _cacheSettings.DefaultExpiration,
            cancellationToken: cancellationToken);
    }
}

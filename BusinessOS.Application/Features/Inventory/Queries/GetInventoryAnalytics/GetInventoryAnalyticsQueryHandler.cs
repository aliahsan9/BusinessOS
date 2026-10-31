using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Inventory.Queries.GetInventoryAnalytics;

public sealed record GetInventoryAnalyticsQuery : IRequest<InventoryAnalyticsResponse>;

public sealed class GetInventoryAnalyticsQueryHandler
    : IRequestHandler<GetInventoryAnalyticsQuery, InventoryAnalyticsResponse>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;

    public GetInventoryAnalyticsQueryHandler(
        IInventoryRepository inventoryRepository,
        IStockTransactionRepository transactionRepository,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings)
    {
        _inventoryRepository = inventoryRepository;
        _transactionRepository = transactionRepository;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
    }

    public async Task<InventoryAnalyticsResponse> Handle(
        GetInventoryAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.InventoryAnalytics(tenantId);

        return await _cache.GetOrSetAsync(
            key,
            async ct => await BuildAnalyticsAsync(ct),
            absoluteExpiration: _cacheSettings.ReportExpiration,
            cancellationToken: cancellationToken);
    }

    private async Task<InventoryAnalyticsResponse> BuildAnalyticsAsync(CancellationToken cancellationToken)
    {
        var inventories = _inventoryRepository.Query();
        var totalProducts = await inventories.CountAsync(cancellationToken);
        var totalStockQuantity = totalProducts == 0
            ? 0
            : await inventories.SumAsync(x => x.CurrentStock, cancellationToken);
        var lowStockCount = await inventories.CountAsync(
            x => x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel,
            cancellationToken);
        var outOfStockCount = await inventories.CountAsync(x => x.CurrentStock <= 0, cancellationToken);
        var inventoryValue = totalProducts == 0
            ? 0
            : await inventories.SumAsync(x => x.CurrentStock * x.Product.CostPrice, cancellationToken);

        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var saleQuery = _transactionRepository.Query()
            .Where(x => x.TransactionType == StockTransactionTypeNames.Sale && x.CreatedAt >= thirtyDaysAgo)
            .GroupBy(x => new { x.ProductId, ProductName = x.Product!.Name })
            .Select(g => new ProductStockMovementDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                TotalQuantityMoved = g.Sum(x => x.Quantity)
            });

        var mostSold = await saleQuery
            .OrderByDescending(x => x.TotalQuantityMoved)
            .ThenBy(x => x.ProductId)
            .Take(5)
            .ToListAsync(cancellationToken);

        var leastSold = await saleQuery
            .Where(x => x.TotalQuantityMoved > 0)
            .OrderBy(x => x.TotalQuantityMoved)
            .ThenBy(x => x.ProductId)
            .Take(5)
            .ToListAsync(cancellationToken);

        var trends = await _transactionRepository.Query()
            .Where(x => x.CreatedAt >= thirtyDaysAgo)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new StockMovementTrendDto
            {
                Date = g.Key,
                TotalIn = g.Where(x =>
                    x.TransactionType == StockTransactionTypeNames.Purchase ||
                    x.TransactionType == StockTransactionTypeNames.Return)
                    .Sum(x => x.Quantity),
                TotalOut = g.Where(x =>
                    x.TransactionType == StockTransactionTypeNames.Sale ||
                    x.TransactionType == StockTransactionTypeNames.Damage)
                    .Sum(x => x.Quantity)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return new InventoryAnalyticsResponse
        {
            TotalProducts = totalProducts,
            TotalStockQuantity = totalStockQuantity,
            LowStockCount = lowStockCount,
            OutOfStockCount = outOfStockCount,
            InventoryValue = Math.Round(inventoryValue, 2),
            MostSoldProducts = mostSold,
            LeastSoldProducts = leastSold,
            StockMovementTrends = trends
        };
    }
}

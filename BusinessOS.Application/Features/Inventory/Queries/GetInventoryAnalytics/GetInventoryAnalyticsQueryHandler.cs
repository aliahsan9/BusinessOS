using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Inventory.Queries.GetInventoryAnalytics;

public sealed record GetInventoryAnalyticsQuery : IRequest<InventoryAnalyticsResponse>;

public sealed class GetInventoryAnalyticsQueryHandler
    : IRequestHandler<GetInventoryAnalyticsQuery, InventoryAnalyticsResponse>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockTransactionRepository _transactionRepository;

    public GetInventoryAnalyticsQueryHandler(
        IInventoryRepository inventoryRepository,
        IStockTransactionRepository transactionRepository)
    {
        _inventoryRepository = inventoryRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<InventoryAnalyticsResponse> Handle(
        GetInventoryAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var inventories = await _inventoryRepository.Query()
            .Select(x => new
            {
                x.ProductId,
                ProductName = x.Product.Name,
                x.CurrentStock,
                x.ReorderLevel,
                CostPrice = x.Product.CostPrice
            })
            .ToListAsync(cancellationToken);

        var totalProducts = inventories.Count;
        var totalStockQuantity = inventories.Sum(x => x.CurrentStock);
        var lowStockCount = inventories.Count(x => x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel);
        var outOfStockCount = inventories.Count(x => x.CurrentStock <= 0);
        var inventoryValue = inventories.Sum(x => x.CurrentStock * x.CostPrice);

        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var saleMovements = await _transactionRepository.Query()
            .Where(x => x.TransactionType == StockTransactionTypeNames.Sale && x.CreatedAt >= thirtyDaysAgo)
            .GroupBy(x => new { x.ProductId, ProductName = x.Product!.Name })
            .Select(g => new ProductStockMovementDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                TotalQuantityMoved = g.Sum(x => x.Quantity)
            })
            .ToListAsync(cancellationToken);

        var mostSold = saleMovements
            .OrderByDescending(x => x.TotalQuantityMoved)
            .Take(5)
            .ToList();

        var leastSold = saleMovements
            .Where(x => x.TotalQuantityMoved > 0)
            .OrderBy(x => x.TotalQuantityMoved)
            .Take(5)
            .ToList();

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

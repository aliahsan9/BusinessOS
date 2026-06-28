using System.Linq.Expressions;
using BusinessOS.Domain.Entities;
using InventoryEntity = BusinessOS.Domain.Entities.Inventory;

namespace BusinessOS.Application.Features.Inventory.Queries;

public static class InventoryProjections
{
    public static readonly Expression<Func<InventoryEntity, InventoryResponse>> ToResponse = x => new InventoryResponse
    {
        Id = x.Id,
        ProductId = x.ProductId,
        ProductName = x.Product.Name,
        ProductSku = x.Product.SKU,
        CurrentStock = x.CurrentStock,
        MinimumStockLevel = x.MinimumStockLevel,
        MaximumStockLevel = x.MaximumStockLevel,
        ReorderLevel = x.ReorderLevel,
        LastUpdated = x.LastUpdated,
        IsLowStock = x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel,
        IsOutOfStock = x.CurrentStock <= 0
    };

    public static readonly Expression<Func<InventoryEntity, InventorySummaryResponse>> ToSummary = x => new InventorySummaryResponse
    {
        Id = x.Id,
        ProductId = x.ProductId,
        ProductName = x.Product.Name,
        ProductSku = x.Product.SKU,
        CurrentStock = x.CurrentStock,
        ReorderLevel = x.ReorderLevel,
        MinimumStockLevel = x.MinimumStockLevel,
        MaximumStockLevel = x.MaximumStockLevel,
        SuggestedReorderQuantity = x.MaximumStockLevel > x.CurrentStock
            ? x.MaximumStockLevel - x.CurrentStock
            : x.ReorderLevel,
        IsLowStock = x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel,
        IsOutOfStock = x.CurrentStock <= 0
    };

    public static readonly Expression<Func<StockTransaction, StockTransactionResponse>> ToTransactionResponse = x => new StockTransactionResponse
    {
        Id = x.Id,
        ProductId = x.ProductId,
        ProductName = x.Product != null ? x.Product.Name : string.Empty,
        TransactionType = x.TransactionType,
        Quantity = x.Quantity,
        PreviousStock = x.PreviousStock,
        NewStock = x.NewStock,
        ReferenceNumber = x.ReferenceNumber,
        Notes = x.Notes,
        CreatedAt = x.CreatedAt
    };
}

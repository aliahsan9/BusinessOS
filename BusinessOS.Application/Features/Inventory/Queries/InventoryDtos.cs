namespace BusinessOS.Application.Features.Inventory.Queries;

public sealed class InventoryResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public string ProductSku { get; init; } = default!;
    public decimal CurrentStock { get; init; }
    public decimal MinimumStockLevel { get; init; }
    public decimal MaximumStockLevel { get; init; }
    public decimal ReorderLevel { get; init; }
    public DateTime LastUpdated { get; init; }
    public bool IsLowStock { get; init; }
    public bool IsOutOfStock { get; init; }
}

public sealed class InventorySummaryResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public string ProductSku { get; init; } = default!;
    public decimal CurrentStock { get; init; }
    public decimal ReorderLevel { get; init; }
    public decimal MinimumStockLevel { get; init; }
    public decimal MaximumStockLevel { get; init; }
    public decimal SuggestedReorderQuantity { get; init; }
    public bool IsLowStock { get; init; }
    public bool IsOutOfStock { get; init; }
}

public sealed class StockTransactionResponse
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public string TransactionType { get; init; } = default!;
    public decimal Quantity { get; init; }
    public decimal PreviousStock { get; init; }
    public decimal NewStock { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class InventoryAnalyticsResponse
{
    public int TotalProducts { get; init; }
    public decimal TotalStockQuantity { get; init; }
    public int LowStockCount { get; init; }
    public int OutOfStockCount { get; init; }
    public decimal InventoryValue { get; init; }
    public IReadOnlyList<ProductStockMovementDto> MostSoldProducts { get; init; } = [];
    public IReadOnlyList<ProductStockMovementDto> LeastSoldProducts { get; init; } = [];
    public IReadOnlyList<StockMovementTrendDto> StockMovementTrends { get; init; } = [];
}

public sealed class ProductStockMovementDto
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = default!;
    public decimal TotalQuantityMoved { get; init; }
}

public sealed class StockMovementTrendDto
{
    public DateTime Date { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
}

public sealed class UpdateStockRequest
{
    public decimal MinimumStockLevel { get; init; }
    public decimal MaximumStockLevel { get; init; }
    public decimal ReorderLevel { get; init; }
}

public sealed class StockAdjustmentRequest
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public string TransactionType { get; init; } = default!;
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
}

public sealed class StockChangeRequest
{
    public Guid ProductId { get; init; }
    public decimal Quantity { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
}

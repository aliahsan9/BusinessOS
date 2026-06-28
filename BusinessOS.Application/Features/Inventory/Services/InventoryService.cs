using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Inventory.Services;

public interface IInventoryService
{
    Task<InventoryResponse> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task UpdateStockLevelsAsync(Guid productId, UpdateStockRequest request, CancellationToken cancellationToken = default);
    Task IncreaseStockAsync(StockChangeRequest request, CancellationToken cancellationToken = default);
    Task DecreaseStockAsync(StockChangeRequest request, CancellationToken cancellationToken = default);
    Task AdjustStockAsync(StockAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<List<InventorySummaryResponse>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);
    Task<List<InventorySummaryResponse>> GetOutOfStockProductsAsync(CancellationToken cancellationToken = default);
    Task<List<InventorySummaryResponse>> GetReorderProductsAsync(CancellationToken cancellationToken = default);
    Task EnsureStockAvailableAsync(IReadOnlyDictionary<Guid, decimal> productQuantities, CancellationToken cancellationToken = default);
    Task DeductForOrderAsync(Order order, IEnumerable<OrderItem> items, CancellationToken cancellationToken = default);
    Task RestoreForCancelledOrderAsync(Order order, IEnumerable<OrderItem> items, CancellationToken cancellationToken = default);
    Task FinalizeOrderAsync(Order order, CancellationToken cancellationToken = default);
    Task CreateInventoryForProductAsync(Product product, CancellationToken cancellationToken = default);
}

public sealed class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        IStockTransactionRepository transactionRepository,
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _inventoryRepository = inventoryRepository;
        _transactionRepository = transactionRepository;
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<InventoryResponse> GetByProductIdAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var inventory = await _inventoryRepository.GetByProductIdAsync(productId, cancellationToken);

        if (inventory is null)
            throw new NotFoundException("Inventory record not found for this product.");

        return MapToResponse(inventory);
    }

    public async Task UpdateStockLevelsAsync(
        Guid productId,
        UpdateStockRequest request,
        CancellationToken cancellationToken = default)
    {
        var inventory = await _inventoryRepository.GetByProductIdForUpdateAsync(productId, cancellationToken);

        if (inventory is null)
            throw new NotFoundException("Inventory record not found for this product.");

        inventory.MinimumStockLevel = request.MinimumStockLevel;
        inventory.MaximumStockLevel = request.MaximumStockLevel;
        inventory.ReorderLevel = request.ReorderLevel;
        inventory.LastUpdated = DateTime.UtcNow;
        inventory.UpdatedAt = DateTime.UtcNow;

        SyncProductStockFields(inventory);
        await _inventoryRepository.UpdateAsync(inventory, cancellationToken);
    }

    public Task IncreaseStockAsync(StockChangeRequest request, CancellationToken cancellationToken = default) =>
        ApplyStockChangeAsync(
            request.ProductId,
            request.Quantity,
            StockTransactionTypeNames.Purchase,
            request.ReferenceNumber,
            null,
            request.Notes,
            cancellationToken);

    public Task DecreaseStockAsync(StockChangeRequest request, CancellationToken cancellationToken = default) =>
        ApplyStockChangeAsync(
            request.ProductId,
            -request.Quantity,
            StockTransactionTypeNames.Sale,
            request.ReferenceNumber,
            null,
            request.Notes,
            cancellationToken);

    public async Task AdjustStockAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!StockTransactionTypeNames.IsValid(request.TransactionType))
            throw new BadRequestException("Invalid transaction type.");

        var quantityChange = request.TransactionType.Equals(
            StockTransactionTypeNames.Sale,
            StringComparison.OrdinalIgnoreCase) ||
            request.TransactionType.Equals(
                StockTransactionTypeNames.Damage,
                StringComparison.OrdinalIgnoreCase)
            ? -request.Quantity
            : request.Quantity;

        await ApplyStockChangeAsync(
            request.ProductId,
            quantityChange,
            request.TransactionType,
            request.ReferenceNumber,
            null,
            request.Notes,
            cancellationToken);
    }

    public async Task<List<InventorySummaryResponse>> GetLowStockProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await _inventoryRepository.GetLowStockAsync(cancellationToken);
        return items.Select(MapToSummary).ToList();
    }

    public async Task<List<InventorySummaryResponse>> GetOutOfStockProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await _inventoryRepository.GetOutOfStockAsync(cancellationToken);
        return items.Select(MapToSummary).ToList();
    }

    public async Task<List<InventorySummaryResponse>> GetReorderProductsAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await _inventoryRepository.GetReorderProductsAsync(cancellationToken);
        return items.Select(MapToSummary).ToList();
    }

    public async Task EnsureStockAvailableAsync(
        IReadOnlyDictionary<Guid, decimal> productQuantities,
        CancellationToken cancellationToken = default)
    {
        foreach (var (productId, quantity) in productQuantities)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(productId, cancellationToken);

            if (inventory is null)
                throw new BadRequestException($"No inventory record exists for product {productId}.");

            if (inventory.CurrentStock < quantity)
            {
                throw new BadRequestException(
                    $"Insufficient stock for product '{inventory.Product.Name}'. " +
                    $"Available: {inventory.CurrentStock}, Requested: {quantity}.");
            }
        }
    }

    public async Task DeductForOrderAsync(
        Order order,
        IEnumerable<OrderItem> items,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            await ApplyStockChangeAsync(
                item.ProductId,
                -item.Quantity,
                StockTransactionTypeNames.Sale,
                order.OrderNumber,
                order.Id,
                $"Stock reserved for order {order.OrderNumber}",
                cancellationToken);
        }
    }

    public async Task RestoreForCancelledOrderAsync(
        Order order,
        IEnumerable<OrderItem> items,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            await ApplyStockChangeAsync(
                item.ProductId,
                item.Quantity,
                StockTransactionTypeNames.Return,
                order.OrderNumber,
                order.Id,
                $"Stock restored for cancelled order {order.OrderNumber}",
                cancellationToken);
        }
    }

    public async Task FinalizeOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _context.StockTransactions
            .Where(x => x.ReferenceId == order.Id && x.TransactionType == StockTransactionTypeNames.Sale)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            transaction.Notes = string.IsNullOrWhiteSpace(transaction.Notes)
                ? $"Finalized for completed order {order.OrderNumber}"
                : $"{transaction.Notes} | Finalized for completed order {order.OrderNumber}";
        }

        if (transactions.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateInventoryForProductAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        var existing = await _inventoryRepository.GetByProductIdAsync(product.Id, cancellationToken);
        if (existing is not null)
            return;

        var inventory = new Domain.Entities.Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            CurrentStock = product.CurrentStock,
            MinimumStockLevel = 0,
            MaximumStockLevel = Math.Max(product.ReorderLevel * 2, product.CurrentStock),
            ReorderLevel = product.ReorderLevel,
            LastUpdated = DateTime.UtcNow
        };

        _context.Inventories.Add(inventory);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyStockChangeAsync(
        Guid productId,
        decimal quantityChange,
        string transactionType,
        string? referenceNumber,
        Guid? referenceId,
        string? notes,
        CancellationToken cancellationToken)
    {
        var inventory = await _inventoryRepository.GetByProductIdForUpdateAsync(productId, cancellationToken);

        if (inventory is null)
            throw new NotFoundException("Inventory record not found for this product.");

        var previousStock = inventory.CurrentStock;
        var newStock = previousStock + quantityChange;

        if (newStock < 0)
        {
            throw new BadRequestException(
                $"Insufficient stock. Available: {previousStock}, Requested change: {Math.Abs(quantityChange)}.");
        }

        inventory.CurrentStock = newStock;
        inventory.LastUpdated = DateTime.UtcNow;
        inventory.UpdatedAt = DateTime.UtcNow;

        SyncProductStockFields(inventory);

        var transaction = new StockTransaction
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            TransactionType = transactionType,
            Quantity = Math.Abs(quantityChange),
            PreviousStock = previousStock,
            NewStock = newStock,
            ReferenceNumber = referenceNumber,
            ReferenceId = referenceId,
            Notes = notes,
            UserId = _currentUserService.UserId
        };

        _context.StockTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void SyncProductStockFields(Domain.Entities.Inventory inventory)
    {
        if (inventory.Product is null)
            return;

        inventory.Product.CurrentStock = inventory.CurrentStock;
        inventory.Product.ReorderLevel = inventory.ReorderLevel;
        inventory.Product.UpdatedAt = DateTime.UtcNow;
    }

    private static InventoryResponse MapToResponse(Domain.Entities.Inventory inventory) =>
        new()
        {
            Id = inventory.Id,
            ProductId = inventory.ProductId,
            ProductName = inventory.Product.Name,
            ProductSku = inventory.Product.SKU,
            CurrentStock = inventory.CurrentStock,
            MinimumStockLevel = inventory.MinimumStockLevel,
            MaximumStockLevel = inventory.MaximumStockLevel,
            ReorderLevel = inventory.ReorderLevel,
            LastUpdated = inventory.LastUpdated,
            IsLowStock = inventory.CurrentStock > 0 && inventory.CurrentStock <= inventory.ReorderLevel,
            IsOutOfStock = inventory.CurrentStock <= 0
        };

    private static InventorySummaryResponse MapToSummary(Domain.Entities.Inventory inventory) =>
        new()
        {
            Id = inventory.Id,
            ProductId = inventory.ProductId,
            ProductName = inventory.Product.Name,
            ProductSku = inventory.Product.SKU,
            CurrentStock = inventory.CurrentStock,
            ReorderLevel = inventory.ReorderLevel,
            MinimumStockLevel = inventory.MinimumStockLevel,
            MaximumStockLevel = inventory.MaximumStockLevel,
            SuggestedReorderQuantity = inventory.MaximumStockLevel > inventory.CurrentStock
                ? inventory.MaximumStockLevel - inventory.CurrentStock
                : inventory.ReorderLevel,
            IsLowStock = inventory.CurrentStock > 0 && inventory.CurrentStock <= inventory.ReorderLevel,
            IsOutOfStock = inventory.CurrentStock <= 0
        };
}

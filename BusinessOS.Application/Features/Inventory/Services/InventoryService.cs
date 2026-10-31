using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        IStockTransactionRepository transactionRepository,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<InventoryService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _transactionRepository = transactionRepository;
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
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
        foreach (var requirement in AggregateQuantities(items))
        {
            await ApplyStockChangeAsync(
                requirement.ProductId,
                -requirement.Quantity,
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
        foreach (var requirement in AggregateQuantities(items))
        {
            await ApplyStockChangeAsync(
                requirement.ProductId,
                requirement.Quantity,
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
        if (quantityChange == 0)
            throw new BadRequestException("Quantity must be positive.");

        var ownsTransaction = !_context.HasActiveTransaction;
        var transaction = ownsTransaction
            ? await _context.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var result = await _context.TryAdjustStockAsync(productId, quantityChange, cancellationToken);
            if (!result.Applied)
            {
                if (result.ProductMissing)
                    throw new NotFoundException("Inventory record not found for this product.");

                _logger.LogWarning(
                    "Inventory reservation failed for product {ProductId}. Available {AvailableStock}, requested change {QuantityChange}",
                    productId,
                    result.AvailableStock,
                    quantityChange);

                throw new BadRequestException(
                    $"Insufficient stock. Available: {result.AvailableStock}, Requested change: {Math.Abs(quantityChange)}.");
            }

            _context.StockTransactions.Add(new StockTransaction
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                TransactionType = transactionType,
                Quantity = Math.Abs(quantityChange),
                PreviousStock = result.PreviousStock,
                NewStock = result.NewStock,
                ReferenceNumber = referenceNumber,
                ReferenceId = referenceId,
                Notes = notes,
                UserId = _currentUserService.UserId
            });

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict while updating stock for product {ProductId}",
                    productId);
                throw new ConflictException(
                    "Stock changed while this update was being saved. Refresh and try again.",
                    ErrorCodes.ConcurrencyConflict);
            }

            var reorder = await _context.Inventories.AsNoTracking()
                .Where(inventory => inventory.ProductId == productId)
                .Select(inventory => new { inventory.ReorderLevel, Name = inventory.Product.Name })
                .FirstOrDefaultAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Stock updated for product {ProductId}: {PreviousStock} -> {NewStock} ({TransactionType})",
                productId,
                result.PreviousStock,
                result.NewStock,
                transactionType);

            if (reorder is not null && result.NewStock > 0 && result.NewStock <= reorder.ReorderLevel)
            {
                _logger.LogWarning(
                    "Low stock warning for product {ProductId} ({ProductName}): {CurrentStock} (reorder level {ReorderLevel})",
                    productId,
                    reorder.Name,
                    result.NewStock,
                    reorder.ReorderLevel);
            }
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private static IReadOnlyList<(Guid ProductId, decimal Quantity)> AggregateQuantities(IEnumerable<OrderItem> items) =>
        items
            .GroupBy(item => item.ProductId)
            .Select(group => (ProductId: group.Key, Quantity: group.Sum(item => item.Quantity)))
            .OrderBy(item => item.ProductId)
            .ToList();

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

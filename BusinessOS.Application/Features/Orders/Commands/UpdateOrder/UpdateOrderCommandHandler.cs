using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Orders.Commands.UpdateOrder;

public sealed class UpdateOrderCommandHandler : IRequestHandler<UpdateOrderCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IBusinessEventService _businessEvents;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<UpdateOrderCommandHandler> _logger;

    public UpdateOrderCommandHandler(
        IApplicationDbContext context,
        IInventoryService inventoryService,
        IBusinessEventService businessEvents,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ILogger<UpdateOrderCommandHandler> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
        _businessEvents = businessEvents;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.OrderItems)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found for update", request.Id);
            throw new NotFoundException("Order not found");
        }

        if (!OrderStatusRules.IsEditable(order.Status))
        {
            throw new ConflictException(
                $"Order in '{order.Status}' status cannot be updated.");
        }

        var previousQuantities = order.OrderItems
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();

        var products = await _context.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (products.Count != productIds.Count)
            throw new BadRequestException("One or more products do not exist.");

        foreach (var product in products.Values)
        {
            if (!product.IsActive)
                throw new BadRequestException($"Product '{product.Name}' is not active.");
        }

        var nextQuantities = request.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var stockIncreases = new Dictionary<Guid, decimal>();
        var stockRestores = new List<OrderItem>();
        var stockDeductions = new List<OrderItem>();

        foreach (var productId in previousQuantities.Keys.Union(nextQuantities.Keys))
        {
            previousQuantities.TryGetValue(productId, out var previousQty);
            nextQuantities.TryGetValue(productId, out var nextQty);
            var delta = nextQty - previousQty;
            if (delta > 0)
            {
                stockIncreases[productId] = delta;
                stockDeductions.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = delta
                });
            }
            else if (delta < 0)
            {
                stockRestores.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = -delta
                });
            }
        }

        if (stockIncreases.Count > 0)
            await _inventoryService.EnsureStockAvailableAsync(stockIncreases, cancellationToken);

        var requestedProductIds = request.Items.Select(x => x.ProductId).ToHashSet();
        decimal totalAmount = 0;

        foreach (var reqItem in request.Items)
        {
            var product = products[reqItem.ProductId];
            var lineTotal = Math.Round(reqItem.Quantity * product.SalePrice, 2);

            var existingItem = order.OrderItems
                .FirstOrDefault(x => x.ProductId == reqItem.ProductId);

            if (existingItem is not null)
            {
                existingItem.Quantity = reqItem.Quantity;
                existingItem.UnitPrice = product.SalePrice;
                existingItem.UnitCost = product.CostPrice;
                existingItem.Total = lineTotal;
            }
            else
            {
                order.OrderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = reqItem.ProductId,
                    Quantity = reqItem.Quantity,
                    UnitPrice = product.SalePrice,
                    UnitCost = product.CostPrice,
                    Total = lineTotal
                });
            }

            totalAmount += lineTotal;
        }

        foreach (var item in order.OrderItems.Where(x => !requestedProductIds.Contains(x.ProductId)).ToList())
            _context.OrderItems.Remove(item);

        var discount = Math.Round(Math.Max(0, request.Discount), 2);
        if (discount > totalAmount)
            throw new BadRequestException("Discount cannot exceed the line subtotal.");

        var tax = await ResolveOrderTaxAsync(totalAmount, discount, request.Tax, cancellationToken);
        var grandTotal = Math.Round(totalAmount - discount + tax, 2);

        if (grandTotal < 0)
            throw new BadRequestException("Order grand total cannot be negative.");

        order.TotalAmount = totalAmount;
        order.Discount = discount;
        order.Tax = tax;
        order.GrandTotal = grandTotal;

        var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            if (stockDeductions.Count > 0)
                await _inventoryService.DeductForOrderAsync(order, stockDeductions, cancellationToken);

            if (stockRestores.Count > 0)
                await _inventoryService.RestoreForCancelledOrderAsync(order, stockRestores, cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);

            _logger.LogWarning(ex, "Concurrency conflict while updating order {OrderId}", order.Id);
            throw new ConflictException(
                "This order was changed by someone else. Refresh and try again.",
                ErrorCodes.ConcurrencyConflict);
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

        await EntityCacheInvalidator.InvalidateOrderAsync(
            _cache,
            _tenantProvider.TenantId,
            order.Id,
            cancellationToken);

        _logger.LogInformation("Updated order {OrderId}", order.Id);

        await PublishEventSafeAsync(
            new BusinessEventRequest(
                ActivityActions.Updated,
                ActivityEntityTypes.Project,
                order.Id,
                order.OrderNumber,
                "Project Updated",
                $"Project {order.OrderNumber} was updated.",
                NotificationTypes.Project),
            cancellationToken);

        return Unit.Value;
    }

    private async Task<decimal> ResolveOrderTaxAsync(
        decimal totalAmount,
        decimal discount,
        decimal requestedTax,
        CancellationToken cancellationToken)
    {
        var taxableBase = Math.Max(0m, totalAmount - discount);
        try
        {
            var taxRates = _context.TaxRates;
            if (taxRates is not null)
            {
                var defaultRate = await taxRates
                    .Where(rate => rate.IsActive && rate.IsDefault)
                    .Select(rate => (decimal?)rate.RatePercent)
                    .FirstOrDefaultAsync(cancellationToken);

                if (defaultRate.HasValue)
                {
                    return Math.Round(
                        taxableBase * (defaultRate.Value / 100m),
                        2,
                        MidpointRounding.AwayFromZero);
                }
            }
        }
        catch (Exception ex) when (ex is NullReferenceException or InvalidOperationException or NotSupportedException)
        {
            _logger.LogDebug(ex, "Tax rate lookup unavailable; using requested tax amount.");
        }

        return Math.Round(Math.Max(0m, requestedTax), 2, MidpointRounding.AwayFromZero);
    }

    private async Task PublishEventSafeAsync(
        BusinessEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _businessEvents.PublishAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish business event for order {OrderId}", request.EntityId);
        }
    }
}

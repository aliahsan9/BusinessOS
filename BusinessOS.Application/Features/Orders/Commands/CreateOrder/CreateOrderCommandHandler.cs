using System.Text.Json;
using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Idempotency;
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

namespace BusinessOS.Application.Features.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IInventoryService _inventoryService;
    private readonly IBusinessEventService _businessEvents;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IApplicationDbContext context,
        IOrderNumberGenerator orderNumberGenerator,
        IInventoryService inventoryService,
        IBusinessEventService businessEvents,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IIdempotencyService idempotency,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _context = context;
        _orderNumberGenerator = orderNumberGenerator;
        _inventoryService = inventoryService;
        _businessEvents = businessEvents;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        IdempotencyRecord? staged = null;
        string? requestHash = null;

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            if (!IdempotencyKeyRules.IsValid(request.IdempotencyKey))
                throw new BadRequestException("Idempotency-Key must be 1-128 visible ASCII characters without spaces.");

            requestHash = CanonicalRequestHasher.OrderCreate(
                tenantId,
                request.CustomerId,
                request.Discount,
                request.Tax,
                request.Items.Select(item => (item.ProductId, item.Quantity)).ToList());

            var outcome = await _idempotency.BeginLocalAsync(
                IdempotencyOperations.OrderCreate,
                request.IdempotencyKey,
                requestHash,
                cancellationToken);

            switch (outcome.Kind)
            {
                case IdempotencyOutcomeKind.Replay:
                    return outcome.ResourceId
                        ?? throw new ConflictException(
                            "The original order could not be replayed.",
                            ErrorCodes.IdempotencyKeyReused);
                case IdempotencyOutcomeKind.Conflict:
                    throw new ConflictException(
                        "This Idempotency-Key was already used for a different order request.",
                        ErrorCodes.IdempotencyKeyReused);
                case IdempotencyOutcomeKind.InProgress:
                    throw new ConflictException(
                        "This order is still processing. Retry the same request.",
                        ErrorCodes.IdempotencyInProgress);
                case IdempotencyOutcomeKind.Started:
                    staged = outcome.Record;
                    break;
            }
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Customer not found.");

        if (!customer.IsActive)
            throw new BadRequestException("Customer is not active.");

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

        var orderItems = new List<OrderItem>();
        decimal totalAmount = 0;

        foreach (var item in request.Items)
        {
            var product = products[item.ProductId];
            var lineTotal = Math.Round(item.Quantity * product.SalePrice, 2);

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = product.SalePrice,
                UnitCost = product.CostPrice,
                Total = lineTotal
            });

            totalAmount += lineTotal;
        }

        var discount = Math.Round(Math.Max(0, request.Discount), 2);
        if (discount > totalAmount)
            throw new BadRequestException("Discount cannot exceed the line subtotal.");

        // Never trust frontend tax when a company tax rate is configured.
        var tax = await ResolveOrderTaxAsync(totalAmount, discount, request.Tax, cancellationToken);
        var grandTotal = Math.Round(totalAmount - discount + tax, 2);

        if (grandTotal < 0)
            throw new BadRequestException("Order grand total cannot be negative.");

        var stockRequirements = request.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        await _inventoryService.EnsureStockAvailableAsync(stockRequirements, cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            OrderNumber = await _orderNumberGenerator.GenerateNextAsync(cancellationToken),
            OrderDate = DateTime.UtcNow,
            Status = OrderStatusNames.Pending,
            TotalAmount = totalAmount,
            Discount = discount,
            Tax = tax,
            GrandTotal = grandTotal,
            OrderItems = orderItems
        };

        var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Orders.Add(order);
            if (staged is not null)
            {
                _idempotency.CompleteLocal(
                    staged,
                    201,
                    JsonSerializer.Serialize(new { id = order.Id }),
                    order.Id);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _inventoryService.DeductForOrderAsync(order, orderItems, cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (staged is not null && DbExceptionClassifier.IsUniqueViolation(ex))
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);

            _context.Detach(order);
            _idempotency.Detach(staged);
            var replay = await _idempotency.BeginLocalAsync(
                IdempotencyOperations.OrderCreate,
                request.IdempotencyKey!,
                requestHash!,
                cancellationToken);

            if (replay.Kind == IdempotencyOutcomeKind.Replay && replay.ResourceId.HasValue)
                return replay.ResourceId.Value;

            if (replay.Record is not null && replay.Kind == IdempotencyOutcomeKind.Started)
                _idempotency.Detach(replay.Record);

            if (replay.Kind == IdempotencyOutcomeKind.Conflict)
            {
                throw new ConflictException(
                    "This Idempotency-Key was already used for a different order request.",
                    ErrorCodes.IdempotencyKeyReused);
            }

            throw new ConflictException(
                "This order is still processing. Retry the same request.",
                ErrorCodes.IdempotencyInProgress);
        }
        catch (Exception ex) when (DbExceptionClassifier.IsUniqueViolation(ex))
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);

            throw new ConflictException(
                "The order could not be saved because of a concurrent update. Refresh and try again.",
                ErrorCodes.ConcurrencyConflict);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            if (staged is not null)
                _idempotency.Detach(staged);
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

        _logger.LogInformation(
            "Created order {OrderNumber} ({OrderId}) for customer {CustomerId}",
            order.OrderNumber,
            order.Id,
            customer.Id);

        await PublishEventSafeAsync(
            new BusinessEventRequest(
                ActivityActions.Created,
                ActivityEntityTypes.Project,
                order.Id,
                order.OrderNumber,
                "Project Created",
                $"Project {order.OrderNumber} was created.",
                NotificationTypes.Project),
            cancellationToken);

        return order.Id;
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

        // Backward compatible: when no company tax rate exists, accept a non-negative requested tax.
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

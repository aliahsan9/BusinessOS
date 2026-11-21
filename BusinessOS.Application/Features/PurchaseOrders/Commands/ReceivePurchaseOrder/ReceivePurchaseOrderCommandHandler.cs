using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.PurchaseOrders.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.ReceivePurchaseOrder;

public sealed class ReceivePurchaseOrderCommandHandler : IRequestHandler<ReceivePurchaseOrderCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserService _user;
    private readonly ILogger<ReceivePurchaseOrderCommandHandler> _logger;

    public ReceivePurchaseOrderCommandHandler(
        IApplicationDbContext context,
        IInventoryService inventoryService,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ICurrentUserService user,
        ILogger<ReceivePurchaseOrderCommandHandler> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _user = user;
        _logger = logger;
    }

    public async Task<Unit> Handle(ReceivePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var purchase = await _context.Purchases
            .Include(x => x.PurchaseItems)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (purchase is null)
            throw new NotFoundException("Purchase order not found.");

        if (!PurchaseOrderStatusRules.CanReceive(purchase.Status))
        {
            throw new BadRequestException(
                $"Purchase order in '{purchase.Status}' status cannot be received. " +
                "Only Approved or PartiallyReceived purchase orders can be received.");
        }

        var lines = request.Lines;
        if (lines is null || lines.Count == 0)
        {
            lines = purchase.PurchaseItems
                .Where(x => !x.IsDeleted && x.ReceivedQuantity < x.Quantity)
                .Select(x => new ReceivePurchaseOrderLineDto(x.Id, x.Quantity - x.ReceivedQuantity))
                .ToList();
        }

        if (lines.Count == 0)
            throw new BadRequestException("Nothing remains to receive on this purchase order.");

        var referenceNumber = purchase.ReferenceNumber ?? purchase.Id.ToString();
        var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            decimal stockIncreased = 0;
            foreach (var line in lines)
            {
                var item = purchase.PurchaseItems.FirstOrDefault(x => x.Id == line.PurchaseItemId && !x.IsDeleted)
                    ?? throw new BadRequestException($"Purchase line {line.PurchaseItemId} was not found.");

                if (line.ReceivedQuantity < 0 || line.DamagedQuantity < 0)
                    throw new BadRequestException("Received and damaged quantities cannot be negative.");

                var remaining = item.Quantity - item.ReceivedQuantity;
                if (line.ReceivedQuantity > remaining)
                    throw new BadRequestException($"Cannot receive {line.ReceivedQuantity}; only {remaining} remaining on line {item.Id}.");

                if (line.ReceivedQuantity > 0)
                {
                    await _inventoryService.IncreaseStockAsync(
                        new StockChangeRequest
                        {
                            ProductId = item.ProductId,
                            Quantity = line.ReceivedQuantity,
                            ReferenceNumber = referenceNumber,
                            Notes = $"Purchase order {purchase.Id} verified receive"
                        },
                        cancellationToken);
                    stockIncreased += line.ReceivedQuantity;
                }

                item.ReceivedQuantity += line.ReceivedQuantity;
                item.DamagedQuantity += line.DamagedQuantity;
                item.ShortageQuantity = Math.Max(0, item.Quantity - item.ReceivedQuantity);
                if (!string.IsNullOrWhiteSpace(line.DiscrepancyNotes))
                    item.ReceivingNotes = line.DiscrepancyNotes.Trim();
            }

            var fullyReceived = purchase.PurchaseItems.Where(x => !x.IsDeleted).All(x => x.ReceivedQuantity >= x.Quantity);
            var newStatus = fullyReceived
                ? PurchaseOrderStatusNames.Received
                : PurchaseOrderStatusNames.PartiallyReceived;

            if (!string.Equals(purchase.Status, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                var transitioned = await _context.TryTransitionPurchaseStatusAsync(
                    purchase.Id,
                    purchase.Status,
                    newStatus,
                    cancellationToken);

                if (!transitioned)
                {
                    throw new ConflictException(
                        "This purchase order was changed by someone else. Refresh and try again.",
                        ErrorCodes.ConcurrencyConflict);
                }

                purchase.Status = newStatus;
            }

            _context.PurchaseReceivingEvents.Add(new PurchaseReceivingEvent
            {
                Id = Guid.NewGuid(),
                PurchaseId = purchase.Id,
                ReceivedByUserId = _user.UserId ?? "system",
                Notes = request.Notes,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(lines),
                StockIncreasedBy = stockIncreased,
                ResultStatus = purchase.Status
            });

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
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

        var tenantId = _tenantProvider.TenantId;
        await EntityCacheInvalidator.InvalidatePurchaseOrderAsync(_cache, tenantId, purchase.Id, cancellationToken);

        foreach (var item in purchase.PurchaseItems)
            await EntityCacheInvalidator.InvalidateInventoryAsync(_cache, tenantId, item.ProductId, cancellationToken);

        _logger.LogInformation(
            "Verified receive for purchase order {PurchaseOrderId}; status {Status}",
            purchase.Id,
            purchase.Status);

        return Unit.Value;
    }
}

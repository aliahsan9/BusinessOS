using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.PurchaseOrders.Services;
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
    private readonly ILogger<ReceivePurchaseOrderCommandHandler> _logger;

    public ReceivePurchaseOrderCommandHandler(
        IApplicationDbContext context,
        IInventoryService inventoryService,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ILogger<ReceivePurchaseOrderCommandHandler> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
        _cache = cache;
        _tenantProvider = tenantProvider;
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
                "Only Approved purchase orders can be received.");
        }

        var referenceNumber = purchase.ReferenceNumber ?? purchase.Id.ToString();

        foreach (var item in purchase.PurchaseItems)
        {
            await _inventoryService.IncreaseStockAsync(
                new StockChangeRequest
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    ReferenceNumber = referenceNumber,
                    Notes = $"Purchase order {purchase.Id} received"
                },
                cancellationToken);
        }

        purchase.Status = PurchaseOrderStatusNames.Received;
        await _context.SaveChangesAsync(cancellationToken);

        var tenantId = _tenantProvider.TenantId;
        await EntityCacheInvalidator.InvalidatePurchaseOrderAsync(_cache, tenantId, purchase.Id, cancellationToken);

        foreach (var item in purchase.PurchaseItems)
            await EntityCacheInvalidator.InvalidateInventoryAsync(_cache, tenantId, item.ProductId, cancellationToken);

        _logger.LogInformation(
            "Received purchase order {PurchaseOrderId} and increased stock for {ItemCount} items",
            purchase.Id,
            purchase.PurchaseItems.Count);

        return Unit.Value;
    }
}

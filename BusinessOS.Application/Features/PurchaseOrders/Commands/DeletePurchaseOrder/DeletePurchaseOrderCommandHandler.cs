using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.PurchaseOrders.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.DeletePurchaseOrder;

public sealed class DeletePurchaseOrderCommandHandler : IRequestHandler<DeletePurchaseOrderCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<DeletePurchaseOrderCommandHandler> _logger;

    public DeletePurchaseOrderCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ILogger<DeletePurchaseOrderCommandHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeletePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var purchase = await _context.Purchases
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (purchase is null)
            throw new NotFoundException("Purchase order not found.");

        if (!PurchaseOrderStatusRules.CanDelete(purchase.Status))
        {
            throw new ConflictException(
                $"Purchase order in '{purchase.Status}' status cannot be deleted.");
        }

        _context.Purchases.Remove(purchase);
        await _context.SaveChangesAsync(cancellationToken);

        await EntityCacheInvalidator.InvalidatePurchaseOrderAsync(
            _cache,
            _tenantProvider.TenantId,
            purchase.Id,
            cancellationToken);

        _logger.LogInformation("Deleted purchase order {PurchaseOrderId}", purchase.Id);

        return Unit.Value;
    }
}

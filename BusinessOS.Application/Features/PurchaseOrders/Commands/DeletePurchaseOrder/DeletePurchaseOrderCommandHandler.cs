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
    private readonly ILogger<DeletePurchaseOrderCommandHandler> _logger;

    public DeletePurchaseOrderCommandHandler(
        IApplicationDbContext context,
        ILogger<DeletePurchaseOrderCommandHandler> logger)
    {
        _context = context;
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

        _logger.LogInformation("Deleted purchase order {PurchaseOrderId}", purchase.Id);

        return Unit.Value;
    }
}

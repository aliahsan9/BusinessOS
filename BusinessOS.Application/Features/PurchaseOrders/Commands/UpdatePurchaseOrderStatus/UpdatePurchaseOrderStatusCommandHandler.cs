using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.PurchaseOrders.Services;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.UpdatePurchaseOrderStatus;

public sealed class UpdatePurchaseOrderStatusCommandHandler
    : IRequestHandler<UpdatePurchaseOrderStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdatePurchaseOrderStatusCommandHandler> _logger;

    public UpdatePurchaseOrderStatusCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdatePurchaseOrderStatusCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        UpdatePurchaseOrderStatusCommand request,
        CancellationToken cancellationToken)
    {
        var purchase = await _context.Purchases
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (purchase is null)
            throw new NotFoundException("Purchase order not found.");

        var newStatus = request.Status.Trim();

        if (string.Equals(newStatus, PurchaseOrderStatusNames.Received, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "Use the receive endpoint to mark a purchase order as Received.");
        }

        PurchaseOrderStatusRules.ValidateTransition(purchase.Status, newStatus);

        purchase.Status = newStatus;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated purchase order {PurchaseOrderId} status to {Status}",
            purchase.Id,
            newStatus);

        return Unit.Value;
    }
}

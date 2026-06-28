using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Orders.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandHandler
    : IRequestHandler<UpdateOrderStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<UpdateOrderStatusCommandHandler> _logger;

    public UpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        IInventoryService inventoryService,
        ILogger<UpdateOrderStatusCommandHandler> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        UpdateOrderStatusCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(x => x.OrderItems)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found for status update", request.Id);
            throw new NotFoundException("Order not found");
        }

        var previousStatus = order.Status;
        OrderStatusRules.ValidateTransition(previousStatus, request.Status);

        if (request.Status.Equals(OrderStatusNames.Cancelled, StringComparison.OrdinalIgnoreCase) &&
            !previousStatus.Equals(OrderStatusNames.Cancelled, StringComparison.OrdinalIgnoreCase) &&
            !previousStatus.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
        {
            await _inventoryService.RestoreForCancelledOrderAsync(order, order.OrderItems, cancellationToken);
        }

        if (request.Status.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase) &&
            !previousStatus.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
        {
            await _inventoryService.FinalizeOrderAsync(order, cancellationToken);
        }

        order.Status = request.Status;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated order {OrderId} status to {Status}",
            order.Id,
            request.Status);

        return Unit.Value;
    }
}

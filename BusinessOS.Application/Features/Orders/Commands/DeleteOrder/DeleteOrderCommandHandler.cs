using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Orders.Commands.DeleteOrder;

public sealed class DeleteOrderCommandHandler : IRequestHandler<DeleteOrderCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IBusinessEventService _businessEvents;
    private readonly ILogger<DeleteOrderCommandHandler> _logger;

    public DeleteOrderCommandHandler(
        IApplicationDbContext context,
        IBusinessEventService businessEvents,
        ILogger<DeleteOrderCommandHandler> logger)
    {
        _context = context;
        _businessEvents = businessEvents;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found for delete", request.Id);
            throw new NotFoundException("Order not found");
        }

        if (string.Equals(order.Status, OrderStatusNames.Processing, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.Status, OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                $"Order in '{order.Status}' status cannot be deleted.");
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted order {OrderId}", order.Id);

        await PublishEventSafeAsync(
            new BusinessEventRequest(
                ActivityActions.Deleted,
                ActivityEntityTypes.Project,
                order.Id,
                order.OrderNumber,
                "Project Deleted",
                $"Project {order.OrderNumber} was deleted.",
                NotificationTypes.Project),
            cancellationToken);

        return Unit.Value;
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

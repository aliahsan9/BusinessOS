using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Notifications.Services;
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
    private readonly IBusinessEventService _businessEvents;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<UpdateOrderStatusCommandHandler> _logger;

    public UpdateOrderStatusCommandHandler(
        IApplicationDbContext context,
        IInventoryService inventoryService,
        IBusinessEventService businessEvents,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ILogger<UpdateOrderStatusCommandHandler> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
        _businessEvents = businessEvents;
        _cache = cache;
        _tenantProvider = tenantProvider;
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

        var canonicalStatus = OrderStatusNames.All.First(status =>
            status.Equals(request.Status, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(previousStatus, canonicalStatus, StringComparison.OrdinalIgnoreCase))
            return Unit.Value;

        var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            var transitioned = await _context.TryTransitionOrderStatusAsync(
                order.Id,
                previousStatus,
                canonicalStatus,
                cancellationToken);

            if (!transitioned)
            {
                throw new ConflictException(
                    "This order was changed by someone else. Refresh and try again.",
                    ErrorCodes.ConcurrencyConflict);
            }

            if (!string.Equals(order.Status, canonicalStatus, StringComparison.OrdinalIgnoreCase))
                order.Status = canonicalStatus;

            if (canonicalStatus.Equals(OrderStatusNames.Cancelled, StringComparison.OrdinalIgnoreCase) &&
                !previousStatus.Equals(OrderStatusNames.Cancelled, StringComparison.OrdinalIgnoreCase) &&
                !previousStatus.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
            {
                await _inventoryService.RestoreForCancelledOrderAsync(order, order.OrderItems, cancellationToken);
            }

            if (canonicalStatus.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase) &&
                !previousStatus.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
            {
                await _inventoryService.FinalizeOrderAsync(order, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
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

        _logger.LogInformation(
            "Updated order {OrderId} status to {Status}",
            order.Id,
            request.Status);

        if (request.Status.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
        {
            await PublishEventSafeAsync(
                new BusinessEventRequest(
                    ActivityActions.Completed,
                    ActivityEntityTypes.Project,
                    order.Id,
                    order.OrderNumber,
                    "Project Completed",
                    $"Project {order.OrderNumber} was completed.",
                    NotificationTypes.Success),
                cancellationToken);

            foreach (var item in order.OrderItems)
            {
                await PublishEventSafeAsync(
                    new BusinessEventRequest(
                        ActivityActions.Completed,
                        ActivityEntityTypes.Task,
                        item.Id,
                        $"Task in {order.OrderNumber}",
                        "Task Completed",
                        $"A task in project {order.OrderNumber} was completed.",
                        NotificationTypes.Task),
                    cancellationToken);
            }
        }
        else
        {
            await PublishEventSafeAsync(
                new BusinessEventRequest(
                    ActivityActions.Updated,
                    ActivityEntityTypes.Project,
                    order.Id,
                    order.OrderNumber,
                    "Project Updated",
                    $"Project {order.OrderNumber} status changed to {request.Status}.",
                    NotificationTypes.Project),
                cancellationToken);
        }

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

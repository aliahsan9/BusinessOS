using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
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
    private readonly IBusinessEventService _businessEvents;
    private readonly ILogger<UpdateOrderCommandHandler> _logger;

    public UpdateOrderCommandHandler(
        IApplicationDbContext context,
        IBusinessEventService businessEvents,
        ILogger<UpdateOrderCommandHandler> logger)
    {
        _context = context;
        _businessEvents = businessEvents;
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
                    Total = lineTotal
                });
            }

            totalAmount += lineTotal;
        }

        foreach (var item in order.OrderItems.Where(x => !requestedProductIds.Contains(x.ProductId)).ToList())
            _context.OrderItems.Remove(item);

        var discount = Math.Round(request.Discount, 2);
        var tax = Math.Round(request.Tax, 2);
        var grandTotal = Math.Round(totalAmount - discount + tax, 2);

        if (grandTotal < 0)
            throw new BadRequestException("Order grand total cannot be negative.");

        order.TotalAmount = totalAmount;
        order.Discount = discount;
        order.Tax = tax;
        order.GrandTotal = grandTotal;

        await _context.SaveChangesAsync(cancellationToken);

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

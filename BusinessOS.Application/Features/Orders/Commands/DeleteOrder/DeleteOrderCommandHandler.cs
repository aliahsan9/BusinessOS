using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Orders.Commands.DeleteOrder;

public sealed class DeleteOrderCommandHandler : IRequestHandler<DeleteOrderCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DeleteOrderCommandHandler> _logger;

    public DeleteOrderCommandHandler(
        IApplicationDbContext context,
        ILogger<DeleteOrderCommandHandler> logger)
    {
        _context = context;
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

        return Unit.Value;
    }
}

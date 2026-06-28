using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IApplicationDbContext context,
        IOrderNumberGenerator orderNumberGenerator,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _context = context;
        _orderNumberGenerator = orderNumberGenerator;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Customer not found.");

        if (!customer.IsActive)
            throw new BadRequestException("Customer is not active.");

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

        var orderItems = new List<OrderItem>();
        decimal totalAmount = 0;

        foreach (var item in request.Items)
        {
            var product = products[item.ProductId];
            var lineTotal = Math.Round(item.Quantity * product.SalePrice, 2);

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = product.SalePrice,
                Total = lineTotal
            });

            totalAmount += lineTotal;
        }

        var discount = Math.Round(request.Discount, 2);
        var tax = Math.Round(request.Tax, 2);
        var grandTotal = Math.Round(totalAmount - discount + tax, 2);

        if (grandTotal < 0)
            throw new BadRequestException("Order grand total cannot be negative.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            OrderNumber = await _orderNumberGenerator.GenerateNextAsync(cancellationToken),
            OrderDate = DateTime.UtcNow,
            Status = OrderStatusNames.Pending,
            TotalAmount = totalAmount,
            Discount = discount,
            Tax = tax,
            GrandTotal = grandTotal,
            OrderItems = orderItems
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created order {OrderNumber} ({OrderId}) for customer {CustomerId}",
            order.OrderNumber,
            order.Id,
            customer.Id);

        return order.Id;
    }
}

using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Application.Features.Quotations.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Quotations.Commands.ConvertQuotationToOrder;

public sealed class ConvertQuotationToOrderCommandHandler
    : IRequestHandler<ConvertQuotationToOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<ConvertQuotationToOrderCommandHandler> _logger;

    public ConvertQuotationToOrderCommandHandler(
        IApplicationDbContext context,
        IOrderNumberGenerator orderNumberGenerator,
        IInventoryService inventoryService,
        ILogger<ConvertQuotationToOrderCommandHandler> logger)
    {
        _context = context;
        _orderNumberGenerator = orderNumberGenerator;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<Guid> Handle(
        ConvertQuotationToOrderCommand request,
        CancellationToken cancellationToken)
    {
        var quotation = await _context.Quotations
            .Include(x => x.QuotationItems)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (quotation is null)
            throw new NotFoundException("Quotation not found.");

        if (!QuotationStatusRules.CanConvertToOrder(quotation.Status))
        {
            throw new ConflictException(
                $"Quotation in '{quotation.Status}' status cannot be converted to an order.");
        }

        if (quotation.QuotationItems.Count == 0)
            throw new BadRequestException("Quotation has no line items.");

        var productIds = quotation.QuotationItems.Select(x => x.ProductId).Distinct().ToList();

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

        var orderItems = quotation.QuotationItems
            .Select(item => new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Total = item.Total
            })
            .ToList();

        var stockRequirements = quotation.QuotationItems
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        await _inventoryService.EnsureStockAvailableAsync(stockRequirements, cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = quotation.CustomerId,
            OrderNumber = await _orderNumberGenerator.GenerateNextAsync(cancellationToken),
            OrderDate = DateTime.UtcNow,
            Status = OrderStatusNames.Pending,
            TotalAmount = quotation.SubTotal,
            Discount = quotation.Discount,
            Tax = quotation.Tax,
            GrandTotal = quotation.GrandTotal,
            OrderItems = orderItems
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        await _inventoryService.DeductForOrderAsync(order, orderItems, cancellationToken);

        _logger.LogInformation(
            "Converted quotation {QuotationId} to order {OrderNumber} ({OrderId})",
            quotation.Id,
            order.OrderNumber,
            order.Id);

        return order.Id;
    }
}

using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.CreatePurchaseOrder;

public sealed class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CreatePurchaseOrderCommandHandler> _logger;

    public CreatePurchaseOrderCommandHandler(
        IApplicationDbContext context,
        ILogger<CreatePurchaseOrderCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var status = request.Status.Trim();

        if (!PurchaseOrderStatusNames.IsValid(status))
            throw new BadRequestException($"Invalid purchase order status '{status}'.");

        if (!string.Equals(status, PurchaseOrderStatusNames.Draft, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, PurchaseOrderStatusNames.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "New purchase orders can only be created in Draft or Pending status.");
        }

        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(x => x.Id == request.SupplierId, cancellationToken);

        if (supplier is null)
            throw new NotFoundException("Supplier not found.");

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

        var purchaseItems = new List<PurchaseItem>();
        decimal totalAmount = 0;

        foreach (var item in request.Items)
        {
            var lineTotal = Math.Round(item.Quantity * item.UnitPrice, 2);

            purchaseItems.Add(new PurchaseItem
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Total = lineTotal
            });

            totalAmount += lineTotal;
        }

        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            PurchaseDate = request.PurchaseDate.ToUniversalTime(),
            TotalAmount = Math.Round(totalAmount, 2),
            Status = status,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? null
                : request.ReferenceNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : request.Notes.Trim(),
            PurchaseItems = purchaseItems
        };

        _context.Purchases.Add(purchase);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created purchase order {PurchaseOrderId} for supplier {SupplierId}",
            purchase.Id,
            supplier.Id);

        return purchase.Id;
    }
}

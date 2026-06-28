using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using BusinessOS.Application.Features.PurchaseOrders.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.PurchaseOrders.Commands.UpdatePurchaseOrder;

public sealed class UpdatePurchaseOrderCommandHandler : IRequestHandler<UpdatePurchaseOrderCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdatePurchaseOrderCommandHandler> _logger;

    public UpdatePurchaseOrderCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdatePurchaseOrderCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var purchase = await _context.Purchases
            .Include(x => x.PurchaseItems)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (purchase is null)
            throw new NotFoundException("Purchase order not found.");

        if (!PurchaseOrderStatusRules.IsEditable(purchase.Status))
        {
            throw new ConflictException(
                $"Purchase order in '{purchase.Status}' status cannot be updated.");
        }

        var status = request.Status.Trim();

        if (!PurchaseOrderStatusNames.IsValid(status))
            throw new BadRequestException($"Invalid purchase order status '{status}'.");

        if (!string.Equals(status, PurchaseOrderStatusNames.Draft, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, PurchaseOrderStatusNames.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "Purchase orders can only be updated while in Draft or Pending status.");
        }

        var supplier = await _context.Suppliers
            .AsNoTracking()
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

        var requestedProductIds = request.Items.Select(x => x.ProductId).ToHashSet();
        decimal totalAmount = 0;

        foreach (var reqItem in request.Items)
        {
            var lineTotal = Math.Round(reqItem.Quantity * reqItem.UnitPrice, 2);

            var existingItem = purchase.PurchaseItems
                .FirstOrDefault(x => x.ProductId == reqItem.ProductId);

            if (existingItem is not null)
            {
                existingItem.Quantity = reqItem.Quantity;
                existingItem.UnitPrice = reqItem.UnitPrice;
                existingItem.Total = lineTotal;
            }
            else
            {
                purchase.PurchaseItems.Add(new PurchaseItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = reqItem.ProductId,
                    Quantity = reqItem.Quantity,
                    UnitPrice = reqItem.UnitPrice,
                    Total = lineTotal
                });
            }

            totalAmount += lineTotal;
        }

        foreach (var item in purchase.PurchaseItems.Where(x => !requestedProductIds.Contains(x.ProductId)).ToList())
            _context.PurchaseItems.Remove(item);

        purchase.SupplierId = request.SupplierId;
        purchase.PurchaseDate = request.PurchaseDate.ToUniversalTime();
        purchase.TotalAmount = Math.Round(totalAmount, 2);
        purchase.Status = status;
        purchase.ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
            ? null
            : request.ReferenceNumber.Trim();
        purchase.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated purchase order {PurchaseOrderId}", purchase.Id);

        return Unit.Value;
    }
}

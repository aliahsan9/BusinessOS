using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Quotations.Queries;
using BusinessOS.Application.Features.Quotations.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Quotations.Commands.UpdateQuotation;

public sealed class UpdateQuotationCommandHandler : IRequestHandler<UpdateQuotationCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdateQuotationCommandHandler> _logger;

    public UpdateQuotationCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdateQuotationCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateQuotationCommand request, CancellationToken cancellationToken)
    {
        var quotation = await _context.Quotations
            .Include(x => x.QuotationItems)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (quotation is null)
            throw new NotFoundException("Quotation not found.");

        if (!QuotationStatusRules.IsEditable(quotation.Status))
        {
            throw new ConflictException(
                $"Quotation in '{quotation.Status}' status cannot be updated.");
        }

        var status = request.Status.Trim();

        if (!QuotationStatusNames.IsValid(status))
            throw new BadRequestException($"Invalid quotation status '{status}'.");

        if (!string.Equals(status, QuotationStatusNames.Draft, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, QuotationStatusNames.Sent, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "Quotations can only be updated while in Draft or Sent status.");
        }

        var customer = await _context.Customers
            .AsNoTracking()
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

        var requestedProductIds = request.Items.Select(x => x.ProductId).ToHashSet();
        decimal subTotal = 0;

        foreach (var reqItem in request.Items)
        {
            var lineTotal = Math.Round(reqItem.Quantity * reqItem.UnitPrice, 2);

            var existingItem = quotation.QuotationItems
                .FirstOrDefault(x => x.ProductId == reqItem.ProductId);

            if (existingItem is not null)
            {
                existingItem.Quantity = reqItem.Quantity;
                existingItem.UnitPrice = reqItem.UnitPrice;
                existingItem.Total = lineTotal;
            }
            else
            {
                quotation.QuotationItems.Add(new QuotationItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = reqItem.ProductId,
                    Quantity = reqItem.Quantity,
                    UnitPrice = reqItem.UnitPrice,
                    Total = lineTotal
                });
            }

            subTotal += lineTotal;
        }

        foreach (var item in quotation.QuotationItems.Where(x => !requestedProductIds.Contains(x.ProductId)).ToList())
            _context.QuotationItems.Remove(item);

        var discount = Math.Round(request.Discount, 2);
        var tax = Math.Round(request.Tax, 2);
        var grandTotal = Math.Round(subTotal - discount + tax, 2);

        if (grandTotal < 0)
            throw new BadRequestException("Quotation grand total cannot be negative.");

        quotation.CustomerId = request.CustomerId;
        quotation.QuotationDate = request.QuotationDate.ToUniversalTime();
        quotation.ExpiryDate = request.ExpiryDate.ToUniversalTime();
        quotation.Status = status;
        quotation.SubTotal = Math.Round(subTotal, 2);
        quotation.Discount = discount;
        quotation.Tax = tax;
        quotation.GrandTotal = grandTotal;
        quotation.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated quotation {QuotationId}", quotation.Id);

        return Unit.Value;
    }
}

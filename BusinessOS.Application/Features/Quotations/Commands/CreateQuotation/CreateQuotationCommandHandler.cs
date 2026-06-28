using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Quotations.Queries;
using BusinessOS.Application.Features.Quotations.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Quotations.Commands.CreateQuotation;

public sealed class CreateQuotationCommandHandler : IRequestHandler<CreateQuotationCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IQuotationNumberGenerator _quotationNumberGenerator;
    private readonly ILogger<CreateQuotationCommandHandler> _logger;

    public CreateQuotationCommandHandler(
        IApplicationDbContext context,
        IQuotationNumberGenerator quotationNumberGenerator,
        ILogger<CreateQuotationCommandHandler> logger)
    {
        _context = context;
        _quotationNumberGenerator = quotationNumberGenerator;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateQuotationCommand request, CancellationToken cancellationToken)
    {
        var status = request.Status.Trim();

        if (!QuotationStatusNames.IsValid(status))
            throw new BadRequestException($"Invalid quotation status '{status}'.");

        if (!string.Equals(status, QuotationStatusNames.Draft, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, QuotationStatusNames.Sent, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "New quotations can only be created in Draft or Sent status.");
        }

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

        var quotationItems = new List<QuotationItem>();
        decimal subTotal = 0;

        foreach (var item in request.Items)
        {
            var lineTotal = Math.Round(item.Quantity * item.UnitPrice, 2);

            quotationItems.Add(new QuotationItem
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Total = lineTotal
            });

            subTotal += lineTotal;
        }

        var discount = Math.Round(request.Discount, 2);
        var tax = Math.Round(request.Tax, 2);
        var grandTotal = Math.Round(subTotal - discount + tax, 2);

        if (grandTotal < 0)
            throw new BadRequestException("Quotation grand total cannot be negative.");

        var quotation = new Quotation
        {
            Id = Guid.NewGuid(),
            QuotationNumber = await _quotationNumberGenerator.GenerateNextAsync(cancellationToken),
            CustomerId = customer.Id,
            QuotationDate = request.QuotationDate.ToUniversalTime(),
            ExpiryDate = request.ExpiryDate.ToUniversalTime(),
            Status = status,
            SubTotal = Math.Round(subTotal, 2),
            Discount = discount,
            Tax = tax,
            GrandTotal = grandTotal,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : request.Notes.Trim(),
            QuotationItems = quotationItems
        };

        _context.Quotations.Add(quotation);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created quotation {QuotationNumber} ({QuotationId}) for customer {CustomerId}",
            quotation.QuotationNumber,
            quotation.Id,
            customer.Id);

        return quotation.Id;
    }
}

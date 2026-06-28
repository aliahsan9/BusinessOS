using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Invoices.Commands.CreateInvoiceFromOrder;

public sealed class CreateInvoiceFromOrderCommandHandler
    : IRequestHandler<CreateInvoiceFromOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IInvoiceNumberGenerator _invoiceNumberGenerator;
    private readonly ILogger<CreateInvoiceFromOrderCommandHandler> _logger;

    public CreateInvoiceFromOrderCommandHandler(
        IApplicationDbContext context,
        IInvoiceNumberGenerator invoiceNumberGenerator,
        ILogger<CreateInvoiceFromOrderCommandHandler> logger)
    {
        _context = context;
        _invoiceNumberGenerator = invoiceNumberGenerator;
        _logger = logger;
    }

    public async Task<Guid> Handle(
        CreateInvoiceFromOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken);

        if (order is null)
            throw new NotFoundException("Order not found.");

        if (!string.Equals(order.Status, OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "Invoices can only be created from completed orders.");
        }

        var existingInvoice = await _context.Invoices
            .AsNoTracking()
            .AnyAsync(x => x.OrderId == request.OrderId, cancellationToken);

        if (existingInvoice)
            throw new ConflictException("An invoice already exists for this order.");

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = await _invoiceNumberGenerator.GenerateNextAsync(cancellationToken),
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            InvoiceDate = DateTime.UtcNow,
            DueDate = request.DueDate.ToUniversalTime(),
            Status = InvoiceStatusNames.Draft,
            SubTotal = order.TotalAmount,
            Discount = order.Discount,
            Tax = order.Tax,
            GrandTotal = order.GrandTotal,
            AmountPaid = 0,
            OutstandingAmount = order.GrandTotal,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : request.Notes.Trim()
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created invoice {InvoiceNumber} ({InvoiceId}) from order {OrderId}",
            invoice.InvoiceNumber,
            invoice.Id,
            order.Id);

        return invoice.Id;
    }
}

using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Audit;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Application.Features.Notifications.Services;
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
    private readonly IBusinessEventService _businessEvents;
    private readonly IEntityAuditService _entityAudit;
    private readonly ILogger<CreateInvoiceFromOrderCommandHandler> _logger;

    public CreateInvoiceFromOrderCommandHandler(
        IApplicationDbContext context,
        IInvoiceNumberGenerator invoiceNumberGenerator,
        IBusinessEventService businessEvents,
        IEntityAuditService entityAudit,
        ILogger<CreateInvoiceFromOrderCommandHandler> logger)
    {
        _context = context;
        _invoiceNumberGenerator = invoiceNumberGenerator;
        _businessEvents = businessEvents;
        _entityAudit = entityAudit;
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

        try
        {
            await _entityAudit.LogChangeAsync(
                ActivityEntityTypes.Invoice,
                invoice.Id,
                ActivityActions.InvoiceCreated,
                null,
                EntityAuditSnapshots.InvoiceSnapshot(invoice),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write entity audit for invoice {InvoiceId}", invoice.Id);
        }

        await PublishEventSafeAsync(
            new BusinessEventRequest(
                ActivityActions.InvoiceCreated,
                ActivityEntityTypes.Invoice,
                invoice.Id,
                $"#{invoice.InvoiceNumber}",
                "Invoice Created",
                $"Created invoice #{invoice.InvoiceNumber}",
                NotificationTypes.Info,
                Link: $"/invoices/{invoice.Id}"),
            cancellationToken);

        return invoice.Id;
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
            _logger.LogWarning(ex, "Failed to publish business event for invoice {InvoiceId}", request.EntityId);
        }
    }
}

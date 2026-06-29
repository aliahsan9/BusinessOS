using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Invoices.Commands.UpdateInvoiceStatus;

public sealed class UpdateInvoiceStatusCommandHandler
    : IRequestHandler<UpdateInvoiceStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IBusinessEventService _businessEvents;
    private readonly ILogger<UpdateInvoiceStatusCommandHandler> _logger;

    public UpdateInvoiceStatusCommandHandler(
        IApplicationDbContext context,
        IBusinessEventService businessEvents,
        ILogger<UpdateInvoiceStatusCommandHandler> logger)
    {
        _context = context;
        _businessEvents = businessEvents;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        UpdateInvoiceStatusCommand request,
        CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (invoice is null)
            throw new NotFoundException("Invoice not found.");

        var newStatus = request.Status.Trim();

        InvoiceStatusRules.ValidateTransition(invoice.Status, newStatus);

        invoice.Status = newStatus;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated invoice {InvoiceId} status to {Status}",
            invoice.Id,
            newStatus);

        if (string.Equals(newStatus, InvoiceStatusNames.Paid, StringComparison.OrdinalIgnoreCase))
        {
            await PublishEventSafeAsync(
                new BusinessEventRequest(
                    ActivityActions.Paid,
                    ActivityEntityTypes.Invoice,
                    invoice.Id,
                    $"#{invoice.InvoiceNumber}",
                    "Invoice Paid",
                    $"Invoice #{invoice.InvoiceNumber} was marked as paid.",
                    NotificationTypes.Success),
                cancellationToken);
        }

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
            _logger.LogWarning(ex, "Failed to publish business event for invoice {InvoiceId}", request.EntityId);
        }
    }
}

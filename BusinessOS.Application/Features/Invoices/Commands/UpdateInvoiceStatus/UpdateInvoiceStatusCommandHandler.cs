using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Invoices.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Invoices.Commands.UpdateInvoiceStatus;

public sealed class UpdateInvoiceStatusCommandHandler
    : IRequestHandler<UpdateInvoiceStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdateInvoiceStatusCommandHandler> _logger;

    public UpdateInvoiceStatusCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdateInvoiceStatusCommandHandler> logger)
    {
        _context = context;
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

        return Unit.Value;
    }
}

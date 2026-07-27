using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Invoices.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Invoices.Commands.UpdateInvoice;

public sealed class UpdateInvoiceCommandHandler : IRequestHandler<UpdateInvoiceCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<UpdateInvoiceCommandHandler> _logger;

    public UpdateInvoiceCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ILogger<UpdateInvoiceCommandHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (invoice is null)
            throw new NotFoundException("Invoice not found.");

        if (!InvoiceStatusRules.IsEditable(invoice.Status))
        {
            throw new ConflictException(
                $"Invoice in '{invoice.Status}' status cannot be updated.");
        }

        invoice.DueDate = request.DueDate.ToUniversalTime();
        invoice.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        await EntityCacheInvalidator.InvalidateInvoiceAsync(
            _cache,
            _tenantProvider.TenantId,
            invoice.Id,
            cancellationToken);

        _logger.LogInformation("Updated invoice {InvoiceId}", invoice.Id);

        return Unit.Value;
    }
}

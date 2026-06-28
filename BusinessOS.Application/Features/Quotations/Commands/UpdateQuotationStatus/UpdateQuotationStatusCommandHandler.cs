using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Quotations.Commands.UpdateQuotationStatus;

public sealed class UpdateQuotationStatusCommandHandler
    : IRequestHandler<UpdateQuotationStatusCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdateQuotationStatusCommandHandler> _logger;

    public UpdateQuotationStatusCommandHandler(
        IApplicationDbContext context,
        ILogger<UpdateQuotationStatusCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        UpdateQuotationStatusCommand request,
        CancellationToken cancellationToken)
    {
        var quotation = await _context.Quotations
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (quotation is null)
            throw new NotFoundException("Quotation not found.");

        var newStatus = request.Status.Trim();

        QuotationStatusRules.ValidateTransition(quotation.Status, newStatus);

        quotation.Status = newStatus;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated quotation {QuotationId} status to {Status}",
            quotation.Id,
            newStatus);

        return Unit.Value;
    }
}

using BusinessOS.Application.Common.Caching;
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
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<UpdateQuotationStatusCommandHandler> _logger;

    public UpdateQuotationStatusCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ILogger<UpdateQuotationStatusCommandHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
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

        await EntityCacheInvalidator.InvalidateQuotationAsync(
            _cache,
            _tenantProvider.TenantId,
            quotation.Id,
            cancellationToken);

        _logger.LogInformation(
            "Updated quotation {QuotationId} status to {Status}",
            quotation.Id,
            newStatus);

        return Unit.Value;
    }
}

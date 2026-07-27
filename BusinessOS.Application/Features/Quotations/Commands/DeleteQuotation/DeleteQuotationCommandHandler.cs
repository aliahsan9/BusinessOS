using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Quotations.Commands.DeleteQuotation;

public sealed class DeleteQuotationCommandHandler : IRequestHandler<DeleteQuotationCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<DeleteQuotationCommandHandler> _logger;

    public DeleteQuotationCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        ILogger<DeleteQuotationCommandHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteQuotationCommand request, CancellationToken cancellationToken)
    {
        var quotation = await _context.Quotations
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (quotation is null)
            throw new NotFoundException("Quotation not found.");

        if (!QuotationStatusRules.CanDelete(quotation.Status))
        {
            throw new ConflictException(
                $"Quotation in '{quotation.Status}' status cannot be deleted.");
        }

        _context.Quotations.Remove(quotation);
        await _context.SaveChangesAsync(cancellationToken);

        await EntityCacheInvalidator.InvalidateQuotationAsync(
            _cache,
            _tenantProvider.TenantId,
            quotation.Id,
            cancellationToken);

        _logger.LogInformation("Deleted quotation {QuotationId}", quotation.Id);

        return Unit.Value;
    }
}

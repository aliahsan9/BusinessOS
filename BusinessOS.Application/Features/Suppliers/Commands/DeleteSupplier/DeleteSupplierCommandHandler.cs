using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Suppliers.Commands.DeleteSupplier;

public sealed class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;

    public DeleteSupplierCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
    }

    public async Task<Unit> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (supplier is null)
            throw new NotFoundException("Supplier not found.");

        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        await EntityCacheInvalidator.InvalidateSupplierAsync(
            _cache,
            _tenantProvider.TenantId,
            request.Id,
            cancellationToken);

        return Unit.Value;
    }
}

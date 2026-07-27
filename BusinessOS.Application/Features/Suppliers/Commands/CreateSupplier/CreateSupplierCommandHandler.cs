using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Suppliers.Commands.CreateSupplier;

public sealed class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;

    public CreateSupplierCommandHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
    }

    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var duplicateExists = await _context.Suppliers
            .AnyAsync(x => x.Email == email, cancellationToken);

        if (duplicateExists)
            throw new ConflictException($"A supplier with email '{email}' already exists.");

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = email,
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            ContactPerson = string.IsNullOrWhiteSpace(request.ContactPerson)
                ? null
                : request.ContactPerson.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? null
                : request.Notes.Trim()
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        await EntityCacheInvalidator.InvalidateSupplierAsync(
            _cache,
            _tenantProvider.TenantId,
            supplier.Id,
            cancellationToken);

        return supplier.Id;
    }
}

using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Suppliers.Commands.UpdateSupplier;

public sealed class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateSupplierCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (supplier is null)
            throw new NotFoundException("Supplier not found.");

        var email = request.Email.Trim();

        var duplicateExists = await _context.Suppliers
            .AnyAsync(x => x.Id != request.Id && x.Email == email, cancellationToken);

        if (duplicateExists)
            throw new ConflictException($"A supplier with email '{email}' already exists.");

        supplier.Name = request.Name.Trim();
        supplier.Email = email;
        supplier.Phone = request.Phone.Trim();
        supplier.Address = request.Address.Trim();
        supplier.ContactPerson = string.IsNullOrWhiteSpace(request.ContactPerson)
            ? null
            : request.ContactPerson.Trim();
        supplier.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

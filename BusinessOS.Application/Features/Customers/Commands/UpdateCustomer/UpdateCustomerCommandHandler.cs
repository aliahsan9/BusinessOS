using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpdateCustomerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Customer not found");

        var email = request.Email.Trim();

        var duplicateExists = await _context.Customers
            .AnyAsync(x => x.Id != request.Id && x.Email == email, cancellationToken);

        if (duplicateExists)
            throw new ConflictException($"A customer with email '{email}' already exists.");

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Email = email;
        customer.PhoneNumber = request.PhoneNumber.Trim();
        customer.Address = request.Address.Trim();
        customer.City = request.City.Trim();
        customer.Country = request.Country.Trim();
        customer.PostalCode = request.PostalCode.Trim();
        customer.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

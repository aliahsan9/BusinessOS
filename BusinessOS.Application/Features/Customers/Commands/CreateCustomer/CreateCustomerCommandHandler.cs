using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateCustomerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var duplicateExists = await _context.Customers
            .AnyAsync(x => x.Email == email, cancellationToken);

        if (duplicateExists)
            throw new ConflictException($"A customer with email '{email}' already exists.");

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            Country = request.Country.Trim(),
            PostalCode = request.PostalCode.Trim(),
            IsActive = true
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}

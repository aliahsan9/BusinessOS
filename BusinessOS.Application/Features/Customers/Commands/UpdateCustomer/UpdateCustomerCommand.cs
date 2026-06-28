using MediatR;

namespace BusinessOS.Application.Features.Customers.Commands.UpdateCustomer;

public record UpdateCustomerCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Address,
    string City,
    string Country,
    string PostalCode,
    bool IsActive
) : IRequest<Unit>;

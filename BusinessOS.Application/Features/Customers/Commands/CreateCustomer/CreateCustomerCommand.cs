using MediatR;

namespace BusinessOS.Application.Features.Customers.Commands.CreateCustomer;

public record CreateCustomerCommand(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Address,
    string City,
    string Country,
    string PostalCode
) : IRequest<Guid>;

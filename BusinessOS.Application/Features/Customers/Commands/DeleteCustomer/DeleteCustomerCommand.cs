using MediatR;

namespace BusinessOS.Application.Features.Customers.Commands.DeleteCustomer;

public record DeleteCustomerCommand(Guid Id) : IRequest<Unit>;

using BusinessOS.Application.Features.Customers.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Customers.Queries.GetCustomerById;

public record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerResponse>;

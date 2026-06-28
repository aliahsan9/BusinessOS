using BusinessOS.Application.Features.Suppliers.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetSupplierById;

public record GetSupplierByIdQuery(Guid Id) : IRequest<SupplierResponse>;

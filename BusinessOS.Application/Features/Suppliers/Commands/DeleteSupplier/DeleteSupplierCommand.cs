using MediatR;

namespace BusinessOS.Application.Features.Suppliers.Commands.DeleteSupplier;

public record DeleteSupplierCommand(Guid Id) : IRequest<Unit>;

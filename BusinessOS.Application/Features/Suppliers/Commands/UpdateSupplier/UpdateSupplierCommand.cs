using MediatR;

namespace BusinessOS.Application.Features.Suppliers.Commands.UpdateSupplier;

public record UpdateSupplierCommand(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Address,
    string? ContactPerson,
    string? Notes
) : IRequest<Unit>;

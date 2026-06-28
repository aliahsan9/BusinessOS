using MediatR;

namespace BusinessOS.Application.Features.Suppliers.Commands.CreateSupplier;

public record CreateSupplierCommand(
    string Name,
    string Email,
    string Phone,
    string Address,
    string? ContactPerson,
    string? Notes
) : IRequest<Guid>;

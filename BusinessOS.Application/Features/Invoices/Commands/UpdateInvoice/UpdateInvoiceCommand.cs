using MediatR;

namespace BusinessOS.Application.Features.Invoices.Commands.UpdateInvoice;

public record UpdateInvoiceCommand(
    Guid Id,
    DateTime DueDate,
    string? Notes
) : IRequest<Unit>;

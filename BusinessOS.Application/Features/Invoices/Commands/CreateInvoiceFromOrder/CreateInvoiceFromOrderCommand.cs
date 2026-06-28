using MediatR;

namespace BusinessOS.Application.Features.Invoices.Commands.CreateInvoiceFromOrder;

public record CreateInvoiceFromOrderCommand(
    Guid OrderId,
    DateTime DueDate,
    string? Notes
) : IRequest<Guid>;

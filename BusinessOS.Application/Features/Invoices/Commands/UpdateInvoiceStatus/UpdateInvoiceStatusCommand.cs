using MediatR;

namespace BusinessOS.Application.Features.Invoices.Commands.UpdateInvoiceStatus;

public record UpdateInvoiceStatusCommand(Guid Id, string Status) : IRequest<Unit>;

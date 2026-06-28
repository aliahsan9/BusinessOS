using MediatR;

namespace BusinessOS.Application.Features.Invoices.Commands.DeleteInvoice;

public record DeleteInvoiceCommand(Guid Id) : IRequest<Unit>;

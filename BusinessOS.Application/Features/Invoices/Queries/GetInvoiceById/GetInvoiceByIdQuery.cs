using BusinessOS.Application.Features.Invoices.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Invoices.Queries.GetInvoiceById;

public record GetInvoiceByIdQuery(Guid Id) : IRequest<InvoiceResponse>;

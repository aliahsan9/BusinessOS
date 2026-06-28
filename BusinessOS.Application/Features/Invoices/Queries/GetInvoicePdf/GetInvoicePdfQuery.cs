using MediatR;

namespace BusinessOS.Application.Features.Invoices.Queries.GetInvoicePdf;

public record GetInvoicePdfQuery(Guid Id) : IRequest<string>;

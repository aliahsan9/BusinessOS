using BusinessOS.Application.Features.Quotations.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Quotations.Queries.GetQuotationById;

public record GetQuotationByIdQuery(Guid Id) : IRequest<QuotationResponse>;

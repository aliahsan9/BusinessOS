using MediatR;

namespace BusinessOS.Application.Features.Quotations.Commands.ConvertQuotationToOrder;

public record ConvertQuotationToOrderCommand(Guid Id) : IRequest<Guid>;

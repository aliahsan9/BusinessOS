using MediatR;

namespace BusinessOS.Application.Features.Quotations.Commands.DeleteQuotation;

public record DeleteQuotationCommand(Guid Id) : IRequest<Unit>;

using MediatR;

namespace BusinessOS.Application.Features.Quotations.Commands.UpdateQuotationStatus;

public record UpdateQuotationStatusCommand(Guid Id, string Status) : IRequest<Unit>;

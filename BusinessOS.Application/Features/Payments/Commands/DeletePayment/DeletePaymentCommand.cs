using MediatR;

namespace BusinessOS.Application.Features.Payments.Commands.DeletePayment;

public record DeletePaymentCommand(Guid Id) : IRequest<Unit>;

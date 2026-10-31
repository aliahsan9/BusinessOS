using MediatR;

namespace BusinessOS.Application.Features.Payments.Commands.RefundPayment;

public record RefundPaymentCommand(
    Guid PaymentId,
    decimal Amount,
    string? Currency,
    string IdempotencyKey
) : IRequest<Guid>;

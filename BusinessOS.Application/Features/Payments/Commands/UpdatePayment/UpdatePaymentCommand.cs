using MediatR;

namespace BusinessOS.Application.Features.Payments.Commands.UpdatePayment;

public record UpdatePaymentCommand(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string PaymentMethod,
    DateTime PaymentDate,
    string? ReferenceNo
) : IRequest<Unit>;

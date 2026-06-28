using BusinessOS.Application.Features.Payments.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Payments.Commands.CreatePayment;

public record CreatePaymentCommand(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string PaymentMethod,
    DateTime PaymentDate,
    string? ReferenceNo
) : IRequest<Guid>;

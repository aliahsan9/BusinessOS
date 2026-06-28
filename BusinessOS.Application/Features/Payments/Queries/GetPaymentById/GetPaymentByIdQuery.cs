using BusinessOS.Application.Features.Payments.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Payments.Queries.GetPaymentById;

public record GetPaymentByIdQuery(Guid Id) : IRequest<PaymentResponse>;

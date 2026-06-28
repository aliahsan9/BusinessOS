using MediatR;

namespace BusinessOS.Application.Features.Orders.Commands.DeleteOrder;

public sealed record DeleteOrderCommand(Guid Id) : IRequest<Unit>;

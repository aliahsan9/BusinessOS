using MediatR;

namespace BusinessOS.Application.Features.Orders.Commands.UpdateOrderStatus;

public sealed record UpdateOrderStatusCommand(Guid Id, string Status) : IRequest<Unit>;

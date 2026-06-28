using BusinessOS.Application.Features.Orders.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Orders.Commands.CreateOrder;

public record CreateOrderCommand(
    Guid CustomerId,
    decimal Discount,
    decimal Tax,
    IReadOnlyList<CreateOrderItemDto> Items
) : IRequest<Guid>;

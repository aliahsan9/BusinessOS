using BusinessOS.Application.Features.Orders.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Orders.Commands.UpdateOrder;

public record UpdateOrderCommand(
    Guid Id,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string CustomerAddress,
    decimal Discount,
    decimal Tax,
    IReadOnlyList<CreateOrderItemDto> Items
) : IRequest<Unit>;

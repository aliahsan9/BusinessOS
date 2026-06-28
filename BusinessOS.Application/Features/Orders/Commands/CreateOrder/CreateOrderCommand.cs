using BusinessOS.Application.Features.Orders.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Orders.Commands.CreateOrder;

public record CreateOrderCommand(
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string CustomerAddress,
    decimal Discount,
    decimal Tax,
    IReadOnlyList<CreateOrderItemDto> Items
) : IRequest<Guid>;

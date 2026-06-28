using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Orders.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Application.Features.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetOrderByIdQueryHandler> _logger;

    public GetOrderByIdQueryHandler(
        IApplicationDbContext context,
        ILogger<GetOrderByIdQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OrderDto> Handle(
        GetOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Where(x => x.Id == request.Id)
            .Select(OrderProjections.ToDetailDto)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found", request.Id);
            throw new NotFoundException("Order not found");
        }

        return order;
    }
}

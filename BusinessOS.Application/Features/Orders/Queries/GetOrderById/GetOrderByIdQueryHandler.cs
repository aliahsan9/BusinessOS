using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Orders.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<GetOrderByIdQueryHandler> _logger;

    public GetOrderByIdQueryHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings,
        ILogger<GetOrderByIdQueryHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    public async Task<OrderDto> Handle(
        GetOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.OrderById(tenantId, request.Id);

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var order = await _context.Orders
                    .AsNoTracking()
                    .Where(x => x.Id == request.Id)
                    .Select(OrderProjections.ToDetailDto)
                    .FirstOrDefaultAsync(ct);

                if (order is null)
                {
                    _logger.LogWarning("Order {OrderId} not found", request.Id);
                    throw new NotFoundException("Order not found");
                }

                return order;
            },
            absoluteExpiration: _cacheSettings.DefaultExpiration,
            cancellationToken: cancellationToken);
    }
}

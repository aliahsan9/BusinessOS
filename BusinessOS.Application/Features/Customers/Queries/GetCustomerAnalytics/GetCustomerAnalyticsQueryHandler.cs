using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Customers.Queries;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Customers.Queries.GetCustomerAnalytics;

public sealed class GetCustomerAnalyticsQueryHandler
    : IRequestHandler<GetCustomerAnalyticsQuery, CustomerAnalyticsResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;

    public GetCustomerAnalyticsQueryHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
    }

    public async Task<CustomerAnalyticsResponse> Handle(
        GetCustomerAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.CustomerAnalytics(tenantId, request.CustomerId);

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var customerExists = await _context.Customers
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == request.CustomerId, ct);

                if (!customerExists)
                    throw new NotFoundException("Customer not found");

                var orders = await _context.Orders
                    .AsNoTracking()
                    .Where(x => x.CustomerId == request.CustomerId)
                    .Select(x => new { x.GrandTotal, x.OrderDate, x.Status })
                    .ToListAsync(ct);

                var totalOrders = orders.Count;
                var totalSpending = orders.Sum(x => x.GrandTotal);
                var completedOrders = orders.Count(x =>
                    x.Status.Equals(OrderStatusNames.Completed, StringComparison.OrdinalIgnoreCase));

                return new CustomerAnalyticsResponse
                {
                    TotalOrders = totalOrders,
                    TotalSpending = totalSpending,
                    AverageOrderValue = totalOrders > 0
                        ? Math.Round(totalSpending / totalOrders, 2)
                        : 0,
                    LastOrderDate = orders.Count > 0
                        ? orders.Max(x => x.OrderDate)
                        : null,
                    TotalCompletedOrders = completedOrders
                };
            },
            absoluteExpiration: _cacheSettings.ReportExpiration,
            cancellationToken: cancellationToken);
    }
}

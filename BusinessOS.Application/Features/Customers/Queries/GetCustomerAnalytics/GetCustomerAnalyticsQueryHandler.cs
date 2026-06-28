using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Customers.Queries;
using BusinessOS.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.Customers.Queries.GetCustomerAnalytics;

public sealed class GetCustomerAnalyticsQueryHandler
    : IRequestHandler<GetCustomerAnalyticsQuery, CustomerAnalyticsResponse>
{
    private readonly IApplicationDbContext _context;

    public GetCustomerAnalyticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerAnalyticsResponse> Handle(
        GetCustomerAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var customerExists = await _context.Customers
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (!customerExists)
            throw new NotFoundException("Customer not found");

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId)
            .Select(x => new { x.GrandTotal, x.OrderDate, x.Status })
            .ToListAsync(cancellationToken);

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
    }
}

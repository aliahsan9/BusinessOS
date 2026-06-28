using BusinessOS.Application.Features.Customers.Queries;
using MediatR;

namespace BusinessOS.Application.Features.Customers.Queries.GetCustomerAnalytics;

public record GetCustomerAnalyticsQuery(Guid CustomerId) : IRequest<CustomerAnalyticsResponse>;

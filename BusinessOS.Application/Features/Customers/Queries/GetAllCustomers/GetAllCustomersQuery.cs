using BusinessOS.Application.Common.Models;
using MediatR;

namespace BusinessOS.Application.Features.Customers.Queries.GetAllCustomers;

public record GetAllCustomersQuery(
    string? Search,
    string? City,
    string? Country,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<Queries.CustomerSummaryResponse>>;

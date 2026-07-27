using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Customers.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Customers.Queries.GetAllCustomers;

public sealed class GetAllCustomersQueryHandler
    : IRequestHandler<GetAllCustomersQuery, PagedResult<CustomerSummaryResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<CustomerSummaryResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["firstname"] = x => x.FullName,
            ["lastname"] = x => x.FullName,
            ["email"] = x => x.Email,
            ["city"] = x => x.City,
            ["country"] = x => x.Country,
            ["createdat"] = x => x.CreatedAt,
            ["isactive"] = x => x.IsActive
        };

    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<GetAllCustomersQueryHandler> _logger;

    public GetAllCustomersQueryHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings,
        ILogger<GetAllCustomersQueryHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    public async Task<PagedResult<CustomerSummaryResponse>> Handle(
        GetAllCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.CustomersAll(
            tenantId,
            page,
            pageSize,
            request.Search,
            request.SortBy,
            request.SortDirection.ToString(),
            request.City,
            request.Country);

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var query = _context.Customers
                    .AsNoTracking()
                    .Select(CustomerProjections.ToSummary);

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var search = request.Search.Trim();
                    query = query.Where(x =>
                        x.FullName.Contains(search) ||
                        x.Email.Contains(search) ||
                        x.PhoneNumber.Contains(search));
                }

                if (!string.IsNullOrWhiteSpace(request.City))
                {
                    var city = request.City.Trim();
                    query = query.Where(x => x.City == city);
                }

                if (!string.IsNullOrWhiteSpace(request.Country))
                {
                    var country = request.Country.Trim();
                    query = query.Where(x => x.Country == country);
                }

                var totalCount = await query.CountAsync(ct);

                var items = await query
                    .ApplySort(
                        request.SortBy,
                        request.SortDirection,
                        SortFields,
                        x => x.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                _logger.LogInformation(
                    "Retrieved {Count} customers (page {Page}, total {Total})",
                    items.Count,
                    page,
                    totalCount);

                return new PagedResult<CustomerSummaryResponse>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            },
            absoluteExpiration: _cacheSettings.DefaultExpiration,
            cancellationToken: cancellationToken);
    }
}

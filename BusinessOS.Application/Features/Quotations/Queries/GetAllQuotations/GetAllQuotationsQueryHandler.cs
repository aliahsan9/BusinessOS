using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Quotations.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Quotations.Queries.GetAllQuotations;

public sealed class GetAllQuotationsQueryHandler
    : IRequestHandler<GetAllQuotationsQuery, PagedResult<QuotationSummaryResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<QuotationSummaryResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["quotationdate"] = x => x.QuotationDate,
            ["expirydate"] = x => x.ExpiryDate,
            ["createdat"] = x => x.CreatedAt,
            ["status"] = x => x.Status,
            ["grandtotal"] = x => x.GrandTotal,
            ["quotationnumber"] = x => x.QuotationNumber,
            ["customername"] = x => x.CustomerName
        };

    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<GetAllQuotationsQueryHandler> _logger;

    public GetAllQuotationsQueryHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings,
        ILogger<GetAllQuotationsQueryHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    public async Task<PagedResult<QuotationSummaryResponse>> Handle(
        GetAllQuotationsQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.QuotationsAll(tenantId, page, pageSize, request.Search, request.Status) +
            $"_c{request.CustomerId}_sb{request.SortBy}_sd{request.SortDirection}";

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var query = _context.Quotations
                    .AsNoTracking()
                    .Select(QuotationProjections.ToSummary);

                if (request.CustomerId.HasValue)
                    query = query.Where(x => x.CustomerId == request.CustomerId.Value);

                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    var status = request.Status.Trim();
                    query = query.Where(x => x.Status == status);
                }

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var search = request.Search.Trim();
                    query = query.Where(x =>
                        x.QuotationNumber.Contains(search) ||
                        x.CustomerName.Contains(search) ||
                        x.Status.Contains(search));
                }

                var totalCount = await query.CountAsync(ct);

                var items = await query
                    .ApplySort(
                        request.SortBy,
                        request.SortDirection,
                        SortFields,
                        x => x.QuotationDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                _logger.LogInformation(
                    "Retrieved {Count} quotations (page {Page}, total {Total})",
                    items.Count,
                    page,
                    totalCount);

                return new PagedResult<QuotationSummaryResponse>
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

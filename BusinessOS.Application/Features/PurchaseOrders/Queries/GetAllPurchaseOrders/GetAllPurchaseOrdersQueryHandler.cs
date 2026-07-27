using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.PurchaseOrders.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.PurchaseOrders.Queries.GetAllPurchaseOrders;

public sealed class GetAllPurchaseOrdersQueryHandler
    : IRequestHandler<GetAllPurchaseOrdersQuery, PagedResult<PurchaseOrderSummaryResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<PurchaseOrderSummaryResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["purchasedate"] = x => x.PurchaseDate,
            ["createdat"] = x => x.CreatedAt,
            ["status"] = x => x.Status,
            ["totalamount"] = x => x.TotalAmount,
            ["suppliername"] = x => x.SupplierName,
            ["referencenumber"] = x => x.ReferenceNumber!
        };

    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<GetAllPurchaseOrdersQueryHandler> _logger;

    public GetAllPurchaseOrdersQueryHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings,
        ILogger<GetAllPurchaseOrdersQueryHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    public async Task<PagedResult<PurchaseOrderSummaryResponse>> Handle(
        GetAllPurchaseOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.PurchaseOrdersAll(tenantId, page, pageSize, request.Search, request.Status) +
            $"_su{request.SupplierId}_sb{request.SortBy}_sd{request.SortDirection}";

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var query = _context.Purchases
                    .AsNoTracking()
                    .Select(PurchaseOrderProjections.ToSummary);

                if (request.SupplierId.HasValue)
                    query = query.Where(x => x.SupplierId == request.SupplierId.Value);

                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    var status = request.Status.Trim();
                    query = query.Where(x => x.Status == status);
                }

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var search = request.Search.Trim();
                    query = query.Where(x =>
                        x.SupplierName.Contains(search) ||
                        x.Status.Contains(search) ||
                        (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
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
                    "Retrieved {Count} purchase orders (page {Page}, total {Total})",
                    items.Count,
                    page,
                    totalCount);

                return new PagedResult<PurchaseOrderSummaryResponse>
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

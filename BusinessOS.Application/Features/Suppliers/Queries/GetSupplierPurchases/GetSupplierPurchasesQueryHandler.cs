using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Suppliers.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Suppliers.Queries.GetSupplierPurchases;

public sealed class GetSupplierPurchasesQueryHandler
    : IRequestHandler<GetSupplierPurchasesQuery, PagedResult<SupplierPurchaseSummaryResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<SupplierPurchaseSummaryResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["purchasedate"] = x => x.PurchaseDate,
            ["totalamount"] = x => x.TotalAmount,
            ["status"] = x => x.Status,
            ["itemcount"] = x => x.ItemCount
        };

    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<GetSupplierPurchasesQueryHandler> _logger;

    public GetSupplierPurchasesQueryHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings,
        ILogger<GetSupplierPurchasesQueryHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    public async Task<PagedResult<SupplierPurchaseSummaryResponse>> Handle(
        GetSupplierPurchasesQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);
        var tenantId = _tenantProvider.TenantId;
        var key = $"{CacheKeys.SupplierPurchases(tenantId, request.SupplierId)}_p{page}_s{pageSize}_sb{request.SortBy ?? "none"}_sd{request.SortDirection}";

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var supplierExists = await _context.Suppliers
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == request.SupplierId, ct);

                if (!supplierExists)
                    throw new NotFoundException("Supplier not found.");

                var query = _context.Purchases
                    .AsNoTracking()
                    .Where(x => x.SupplierId == request.SupplierId)
                    .Select(SupplierProjections.ToPurchaseSummary);

                var totalCount = await query.CountAsync(ct);

                var items = await query
                    .ApplySort(
                        request.SortBy,
                        request.SortDirection,
                        SortFields,
                        x => x.PurchaseDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                _logger.LogInformation(
                    "Retrieved {Count} purchases for supplier {SupplierId}",
                    items.Count,
                    request.SupplierId);

                return new PagedResult<SupplierPurchaseSummaryResponse>
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

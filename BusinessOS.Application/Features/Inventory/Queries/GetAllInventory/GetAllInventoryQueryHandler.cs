using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using InventoryEntity = BusinessOS.Domain.Entities.Inventory;

namespace BusinessOS.Application.Features.Inventory.Queries.GetAllInventory;

public sealed record GetAllInventoryQuery(
    string? Search,
    bool? LowStock,
    bool? OutOfStock,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection
) : IRequest<PagedResult<InventorySummaryResponse>>;

public sealed class GetAllInventoryQueryHandler
    : IRequestHandler<GetAllInventoryQuery, PagedResult<InventorySummaryResponse>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<InventorySummaryResponse, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["currentstock"] = x => x.CurrentStock,
            ["reorderlevel"] = x => x.ReorderLevel,
            ["productname"] = x => x.ProductName,
            ["productsku"] = x => x.ProductSku
        };

    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;

    public GetAllInventoryQueryHandler(
        IInventoryRepository inventoryRepository,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings)
    {
        _inventoryRepository = inventoryRepository;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
    }

    public async Task<PagedResult<InventorySummaryResponse>> Handle(
        GetAllInventoryQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);
        var tenantId = _tenantProvider.TenantId;
        var key = $"{CacheKeys.InventoryAll(tenantId, page, pageSize, request.Search)}_lo{request.LowStock}_oo{request.OutOfStock}_sb{request.SortBy ?? "none"}_sd{request.SortDirection}";

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var query = _inventoryRepository.Query().Select(InventoryProjections.ToSummary);

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var search = request.Search.Trim();
                    query = query.Where(x =>
                        x.ProductName.Contains(search) ||
                        x.ProductSku.Contains(search));
                }

                if (request.LowStock == true)
                    query = query.Where(x => x.IsLowStock);

                if (request.OutOfStock == true)
                    query = query.Where(x => x.IsOutOfStock);

                var totalCount = await query.CountAsync(ct);

                var items = await query
                    .ApplySort(
                        request.SortBy,
                        request.SortDirection,
                        SortFields,
                        x => x.ProductName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                return new PagedResult<InventorySummaryResponse>
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

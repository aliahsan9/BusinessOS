using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Extensions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Categories.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Categories.Queries.GetAllCategories;

public sealed class GetAllCategoriesQueryHandler
    : IRequestHandler<GetAllCategoriesQuery, PagedResult<CategoryDto>>
{
    private static readonly Dictionary<string, System.Linq.Expressions.Expression<Func<CategoryDto, object>>> SortFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = x => x.Name,
            ["description"] = x => x.Description!
        };

    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<GetAllCategoriesQueryHandler> _logger;

    public GetAllCategoriesQueryHandler(
        IApplicationDbContext context,
        ICacheService cache,
        ITenantProvider tenantProvider,
        IOptions<CacheSettings> cacheSettings,
        ILogger<GetAllCategoriesQueryHandler> logger)
    {
        _context = context;
        _cache = cache;
        _tenantProvider = tenantProvider;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;
    }

    public async Task<PagedResult<CategoryDto>> Handle(
        GetAllCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = PaginationParams.Normalize(request.Page, request.PageSize);
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.CategoriesAll(
            tenantId,
            page,
            pageSize,
            request.Search,
            request.SortBy,
            request.SortDirection.ToString());

        return await _cache.GetOrSetAsync(
            key,
            async ct =>
            {
                var query = _context.Categories
                    .AsNoTracking()
                    .Select(x => new CategoryDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Description = x.Description
                    });

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var search = request.Search.Trim();
                    query = query.Where(x =>
                        x.Name.Contains(search) ||
                        (x.Description != null && x.Description.Contains(search)));
                }

                var totalCount = await query.CountAsync(ct);

                var items = await query
                    .ApplySort(
                        request.SortBy,
                        request.SortDirection,
                        SortFields,
                        x => x.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                _logger.LogInformation(
                    "Retrieved {Count} categories (page {Page}, total {Total})",
                    items.Count,
                    page,
                    totalCount);

                return new PagedResult<CategoryDto>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            },
            absoluteExpiration: _cacheSettings.StaticDataExpiration,
            cancellationToken: cancellationToken);
    }
}

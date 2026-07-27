using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Expenses.Queries.GetAllExpenseCategories;

public sealed class GetAllExpenseCategoriesQueryHandler
    : IRequestHandler<GetAllExpenseCategoriesQuery, IReadOnlyList<ExpenseCategoryResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;

    public GetAllExpenseCategoriesQueryHandler(
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

    public async Task<IReadOnlyList<ExpenseCategoryResponse>> Handle(
        GetAllExpenseCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.ExpenseCategoriesAll(tenantId) + $"_a{request.ActiveOnly}";

        return await _cache.GetOrSetAsync<List<ExpenseCategoryResponse>>(
            key,
            async ct =>
            {
                var query = _context.ExpenseCategories.AsNoTracking();

                if (request.ActiveOnly == true)
                    query = query.Where(x => x.IsActive);

                return await query
                    .OrderBy(x => x.Name)
                    .Select(ExpenseProjections.ToCategoryResponse)
                    .ToListAsync(ct);
            },
            absoluteExpiration: _cacheSettings.StaticDataExpiration,
            cancellationToken: cancellationToken);
    }
}

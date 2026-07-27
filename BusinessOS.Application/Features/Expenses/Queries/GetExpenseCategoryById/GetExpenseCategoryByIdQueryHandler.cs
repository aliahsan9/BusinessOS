using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusinessOS.Application.Features.Expenses.Queries.GetExpenseCategoryById;

public sealed class GetExpenseCategoryByIdQueryHandler
    : IRequestHandler<GetExpenseCategoryByIdQuery, ExpenseCategoryResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly ITenantProvider _tenantProvider;
    private readonly CacheSettings _cacheSettings;

    public GetExpenseCategoryByIdQueryHandler(
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

    public async Task<ExpenseCategoryResponse> Handle(
        GetExpenseCategoryByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.TenantId;
        var key = CacheKeys.ExpenseCategoryById(tenantId, request.Id);

        return await _cache.GetOrSetAsync(
            key,
            async ct => await _context.ExpenseCategories
                .AsNoTracking()
                .Where(x => x.Id == request.Id)
                .Select(ExpenseProjections.ToCategoryResponse)
                .FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("Expense category not found."),
            absoluteExpiration: _cacheSettings.StaticDataExpiration,
            cancellationToken: cancellationToken);
    }
}

using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class UserAnalyticsService : IUserAnalyticsService
{
    private readonly BusinessOSDbContext _context;
    private readonly ITenantProvider _tenantProvider;

    public UserAnalyticsService(
        BusinessOSDbContext context,
        ITenantProvider tenantProvider)
    {
        _context = context;
        _tenantProvider = tenantProvider;
    }

    public async Task<int> GetActiveUserCountAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantProvider.HasTenant())
            return 0;

        var tenantId = _tenantProvider.TenantId;

        return await _context.Set<ApplicationUser>()
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);
    }
}

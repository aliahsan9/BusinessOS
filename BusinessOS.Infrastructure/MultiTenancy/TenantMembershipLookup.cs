using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.MultiTenancy;

public sealed class TenantMembershipLookup : ITenantMembershipLookup
{
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;

    public TenantMembershipLookup(IDbContextFactory<BusinessOSDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<TenantMembership?> FindByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.TenantId, x.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? null : new TenantMembership(user.TenantId, user.IsActive);
    }
}

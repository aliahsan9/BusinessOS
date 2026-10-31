using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class AuthorizationVersionService : IAuthorizationVersionService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;

    public AuthorizationVersionService(IDbContextFactory<BusinessOSDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task BumpUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.SecurityVersion++;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task BumpUsersWithRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var userIds = await db.RbacUserRoles
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
        {
            return;
        }

        var users = await db.Users.Where(x => userIds.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            user.SecurityVersion++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

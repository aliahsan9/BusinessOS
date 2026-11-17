using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

internal static class Ops
{
    public static void Require(ICurrentUserService user, params string[] codes)
    {
        if (codes.Length == 0 || codes.Any(user.HasPermission))
            return;
        throw new UnauthorizedException("You do not have permission to perform this action.");
    }

    public static Guid Tenant(ICurrentUserService user) =>
        user.TenantId ?? throw new BadRequestException("Tenant context is required.");

    public static string User(ICurrentUserService user) =>
        user.UserId ?? throw new UnauthorizedException("User context is required.");

    public static async Task<PagedResult<T>> PageAsync<T>(this IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public static void Track(IApplicationDbContext db, string userId, string action, string type, Guid id, string name)
    {
        db.Activities.Add(new Activity
        {
            UserId = userId,
            UserName = userId,
            Action = action,
            EntityType = type,
            EntityId = id,
            EntityName = name.Length > 200 ? name[..200] : name
        });
    }

    public static string Number(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    public static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BadRequestException($"{name} is required.");
        return value.Trim();
    }
}

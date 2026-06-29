using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Activities.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class ActivityService : IActivityService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ActivityService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ActivityResponse>> GetActivitiesAsync(
        ActivityQueryParams query,
        CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = PaginationParams.Normalize(query.Page, query.PageSize);

        var activitiesQuery = _context.Activities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entityType = query.EntityType.Trim();
            activitiesQuery = activitiesQuery.Where(x => x.EntityType == entityType);
        }

        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value.ToUniversalTime();
            activitiesQuery = activitiesQuery.Where(x => x.CreatedAt >= from);
        }

        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value.ToUniversalTime().Date.AddDays(1);
            activitiesQuery = activitiesQuery.Where(x => x.CreatedAt < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            activitiesQuery = activitiesQuery.Where(x =>
                x.UserName.Contains(term) ||
                x.EntityName.Contains(term) ||
                x.Action.Contains(term));
        }

        var totalCount = await activitiesQuery.CountAsync(cancellationToken);

        var items = await activitiesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => MapActivity(x))
            .ToListAsync(cancellationToken);

        return new PagedResult<ActivityResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<IReadOnlyList<ActivityResponse>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 100);

        return await _context.Activities
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(normalizedLimit)
            .Select(x => MapActivity(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ActivityResponse> LogAsync(
        LogActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedException("User context is required.");

        var userName = ResolveUserName();

        var duplicateExists = await _context.Activities
            .AnyAsync(x =>
                x.UserId == userId &&
                x.Action == request.Action &&
                x.EntityType == request.EntityType &&
                x.EntityId == request.EntityId &&
                x.CreatedAt >= DateTime.UtcNow.AddSeconds(-10),
                cancellationToken);

        if (duplicateExists)
        {
            var existing = await _context.Activities
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.Action == request.Action &&
                    x.EntityType == request.EntityType &&
                    x.EntityId == request.EntityId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstAsync(cancellationToken);

            return MapActivity(existing);
        }

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = userName,
            Action = request.Action.Trim(),
            EntityType = request.EntityType.Trim(),
            EntityId = request.EntityId,
            EntityName = request.EntityName.Trim(),
            Metadata = request.Metadata
        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapActivity(activity);
    }

    private string ResolveUserName()
    {
        var email = _currentUserService.Email;
        if (string.IsNullOrWhiteSpace(email))
            return "User";

        return email.Split('@')[0];
    }

    private static ActivityResponse MapActivity(Activity activity) =>
        new()
        {
            Id = activity.Id,
            UserId = activity.UserId,
            UserName = activity.UserName,
            Action = activity.Action,
            EntityType = activity.EntityType,
            EntityId = activity.EntityId,
            EntityName = activity.EntityName,
            Metadata = activity.Metadata,
            CreatedAt = activity.CreatedAt,
            Description = BuildDescription(activity.UserName, activity.Action, activity.EntityType, activity.EntityName)
        };

    internal static string BuildDescription(
        string userName,
        string action,
        string entityType,
        string entityName) =>
        $"{userName} {action.ToLowerInvariant()} {FormatEntityLabel(entityType)} {entityName}";

    private static string FormatEntityLabel(string entityType) =>
        entityType switch
        {
            "Project" => "project",
            "Task" => "task",
            "Customer" => "customer",
            "Invoice" => "invoice",
            "Expense" => "expense",
            "Settings" => "settings",
            _ => entityType.ToLowerInvariant()
        };
}

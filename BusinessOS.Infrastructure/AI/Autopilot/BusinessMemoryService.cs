using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Autopilot;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Autopilot;

public sealed class BusinessMemoryService : IBusinessMemoryService
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "policy_summary", "product_knowledge", "supplier_preference",
        "communication_style", "support_procedure", "workflow_outcome"
    };

    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ITenantProvider _tenant;

    public BusinessMemoryService(BusinessOSDbContext db, ICurrentUserService user, ITenantProvider tenant)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<BusinessMemoryDto>> ListAsync(string? kind = null, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var query = _db.BusinessMemoryEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(kind))
            query = query.Where(x => x.Kind == kind);
        var now = DateTime.UtcNow;
        var rows = await query
            .Where(x => x.ExpiresAtUtc == null || x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<BusinessMemoryDto> UpsertAsync(BusinessMemoryWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        Validate(request);
        // Memory never silently rewrites Autopilot policy or grants permissions.
        if (LooksLikePermissionGrant(request.Content))
            throw new BadRequestException("Business memory cannot grant permissions or rewrite Autopilot policy.");

        var row = new BusinessMemoryEntry
        {
            TenantId = _tenant.TenantId,
            Kind = request.Kind.Trim().ToLowerInvariant(),
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            SourceAttribution = request.SourceAttribution.Trim(),
            SourceEntityType = request.SourceEntityType,
            SourceEntityId = request.SourceEntityId,
            ExpiresAtUtc = request.ExpiresAtUtc,
            IsTrusted = true,
            CreatedByUserId = _user.UserId ?? ""
        };
        _db.BusinessMemoryEntries.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<BusinessMemoryDto> CorrectAsync(Guid id, BusinessMemoryWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        Validate(request);
        if (LooksLikePermissionGrant(request.Content))
            throw new BadRequestException("Business memory cannot grant permissions or rewrite Autopilot policy.");

        var row = await _db.BusinessMemoryEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Memory entry was not found.");
        row.Kind = request.Kind.Trim().ToLowerInvariant();
        row.Title = request.Title.Trim();
        row.Content = request.Content.Trim();
        row.SourceAttribution = request.SourceAttribution.Trim();
        row.SourceEntityType = request.SourceEntityType;
        row.SourceEntityId = request.SourceEntityId;
        row.ExpiresAtUtc = request.ExpiresAtUtc;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await _db.BusinessMemoryEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Memory entry was not found.");
        row.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessMemoryDto>> RetrieveForContextAsync(
        string query,
        int take = 8,
        CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        take = Math.Clamp(take, 1, 20);
        var now = DateTime.UtcNow;
        var q = (query ?? "").Trim();
        var rows = await _db.BusinessMemoryEntries.AsNoTracking()
            .Where(x => x.ExpiresAtUtc == null || x.ExpiresAtUtc > now)
            .Where(x => x.IsTrusted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        // SQL source of truth with simple relevance; Qdrant vector id reserved for future embedding sync.
        // Tenant filter is enforced by EF query filters — never search across tenants.
        IEnumerable<BusinessMemoryEntry> ranked = rows;
        if (!string.IsNullOrWhiteSpace(q))
        {
            ranked = rows
                .Select(x => (Row: x, Score: Score(x, q)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Row);
        }

        return ranked.Take(take).Select(Map).ToList();
    }

    private static int Score(BusinessMemoryEntry entry, string query)
    {
        var score = 0;
        foreach (var token in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (entry.Title.Contains(token, StringComparison.OrdinalIgnoreCase))
                score += 3;
            if (entry.Content.Contains(token, StringComparison.OrdinalIgnoreCase))
                score += 1;
            if (entry.Kind.Contains(token, StringComparison.OrdinalIgnoreCase))
                score += 2;
        }
        return score;
    }

    private static bool LooksLikePermissionGrant(string content)
    {
        var text = content.ToLowerInvariant();
        return text.Contains("grant permission")
            || text.Contains("set mode to controlled")
            || text.Contains("disable approval")
            || text.Contains("emergency stop off")
            || text.Contains("allow all tools")
            || text.Contains("ignore spending limit");
    }

    private static void Validate(BusinessMemoryWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Kind) || !AllowedKinds.Contains(request.Kind))
            throw new BadRequestException("Memory kind is invalid.");
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            throw new BadRequestException("Title and content are required.");
        if (string.IsNullOrWhiteSpace(request.SourceAttribution))
            throw new BadRequestException("Source attribution is required.");
        if (request.Content.Length > 8000)
            throw new BadRequestException("Memory content is too long.");
    }

    private void Require(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException("Missing permission.");
    }

    private static BusinessMemoryDto Map(BusinessMemoryEntry row) => new()
    {
        Id = row.Id,
        Kind = row.Kind,
        Title = row.Title,
        Content = row.Content,
        SourceAttribution = row.SourceAttribution,
        SourceEntityType = row.SourceEntityType,
        SourceEntityId = row.SourceEntityId,
        ExpiresAtUtc = row.ExpiresAtUtc,
        IsTrusted = row.IsTrusted,
        CreatedAt = row.CreatedAt
    };
}

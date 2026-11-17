using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Platform;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Application.Features.VectorSearch.Models;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Domain.Companies;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Infrastructure.Services;

public sealed class BusinessTypeCatalogService : IBusinessTypeCatalogService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _factory;

    public BusinessTypeCatalogService(IDbContextFactory<BusinessOSDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<BusinessTypeTemplateDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.BusinessTypeTemplates.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return BusinessTypeCatalog.Defaults.Select(ToDto).ToList();
        }

        return rows.Select(x => new BusinessTypeTemplateDto
        {
            Key = x.Key,
            Name = x.Name,
            Description = x.Description,
            Modules = ModuleList.Parse(x.ModulesJson)
        }).ToList();
    }

    public async Task<string?> ModulesJsonForKeyAsync(string? key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var normalized = key.Trim();
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var stored = await db.BusinessTypeTemplates.AsNoTracking()
            .Where(x => x.IsActive && x.Key == normalized)
            .Select(x => x.ModulesJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is not null)
            return stored;

        return BusinessTypeCatalog.Defaults
            .FirstOrDefault(x => x.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ?.ModulesJson;
    }

    private static BusinessTypeTemplateDto ToDto(BusinessTypeDefinition definition) => new()
    {
        Key = definition.Key,
        Name = definition.Name,
        Description = definition.Description,
        Modules = ModuleList.Parse(definition.ModulesJson)
    };
}

public sealed class CompanyAnnouncementService : ICompanyAnnouncementService
{
    private readonly IDbContextFactory<BusinessOSDbContext> _factory;
    private readonly ICurrentUserService _currentUser;

    public CompanyAnnouncementService(
        IDbContextFactory<BusinessOSDbContext> factory,
        ICurrentUserService currentUser)
    {
        _factory = factory;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AnnouncementDto>> ListVisibleAsync(CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.TenantId
            ?? throw new BadRequestException("Company context is required.");

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.PlatformAnnouncements.AsNoTracking()
            .Include(x => x.Targets)
            .Where(x => !x.IsDeleted && x.IsActive && x.IsPublished)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        return rows
            .Where(x => x.IsVisibleTo(companyId, now))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AnnouncementDto
            {
                Id = x.Id,
                Title = x.Title,
                Body = x.Body,
                IsPublished = x.IsPublished,
                IsActive = x.IsActive,
                StartsAt = x.StartsAt,
                EndsAt = x.EndsAt,
                TargetAllCompanies = x.TargetAllCompanies,
                CreatedAt = x.CreatedAt
            })
            .ToList();
    }
}

public sealed class CompanyKnowledgeService : ICompanyKnowledgeService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenant;
    private readonly ITenantLimitService _limits;
    private readonly IVectorStore _vectors;

    public CompanyKnowledgeService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITenantProvider tenant,
        ITenantLimitService limits,
        IVectorStore vectors)
    {
        _db = db;
        _currentUser = currentUser;
        _tenant = tenant;
        _limits = limits;
        _vectors = vectors;
    }

    public async Task<IReadOnlyList<KnowledgeDocumentDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.AiDocuments.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new KnowledgeDocumentDto
            {
                Id = x.Id,
                Title = x.Title,
                DocumentType = x.DocumentType,
                IsIndexed = x.IsIndexed,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var companyId = RequireCompany();
        var document = await _db.AiDocuments.FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");

        var chunks = await _db.AiDocumentChunks.Where(x => x.DocumentId == documentId).ToListAsync(cancellationToken);
        if (chunks.Count > 0)
            _db.AiDocumentChunks.RemoveRange(chunks);
        _db.AiDocuments.Remove(document);
        await _db.SaveChangesAsync(cancellationToken);

        await _vectors.DeleteByEntityAsync(companyId, VectorEntityTypes.Document, documentId, cancellationToken);
    }

    public async Task<CompanyResourceDto> GetResourcesAsync(CancellationToken cancellationToken = default)
    {
        var companyId = RequireCompany();
        var limits = await _limits.GetLimitsAsync(companyId, cancellationToken);
        var usage = await _db.TenantUsages.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var tenant = await _db.Tenants.AsNoTracking().FirstAsync(x => x.Id == companyId, cancellationToken);
        var members = await _db.CompanyMemberships.IgnoreQueryFilters().CountAsync(
            x => x.TenantId == companyId && x.IsActive && !x.IsDeleted, cancellationToken);

        return new CompanyResourceDto
        {
            MemberCount = members,
            MaxUsers = limits.MaxUsers,
            AiRequestsUsed = usage?.AiRequestsUsed ?? 0,
            MaxAiRequests = limits.MaxAiRequests,
            StorageUsedMb = usage?.StorageUsedMb ?? 0,
            MaxStorageMb = limits.MaxStorageMb,
            KnowledgeDocuments = await _db.AiDocuments.CountAsync(cancellationToken),
            StoredFiles = await _db.StoredFiles.CountAsync(cancellationToken),
            EnabledModules = ModuleList.Parse(tenant.EnabledModulesJson).ToList()
        };
    }

    private Guid RequireCompany() =>
        _currentUser.TenantId ?? (_tenant.HasTenant() ? _tenant.TenantId : throw new BadRequestException("Company context is required."));
}

public sealed class LocalCompanyFileStore : ICompanyFileStore
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenant;
    private readonly ITenantLimitService _limits;
    private readonly IWebHostEnvironment _environment;

    public LocalCompanyFileStore(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ITenantProvider tenant,
        ITenantLimitService limits,
        IWebHostEnvironment environment)
    {
        _db = db;
        _currentUser = currentUser;
        _tenant = tenant;
        _limits = limits;
        _environment = environment;
    }

    public async Task<IReadOnlyList<StoredFileDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.StoredFiles.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new StoredFileDto
            {
                Id = x.Id,
                FileName = x.FileName,
                ContentType = x.ContentType,
                SizeBytes = x.SizeBytes,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StoredFileDto> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var companyId = RequireCompany();
        var userId = _currentUser.UserId ?? throw new UnauthorizedException("Authentication is required.");
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        try
        {
            CompanyFileRules.Validate(fileName, buffer.Length);
        }
        catch (InvalidOperationException ex)
        {
            throw new BadRequestException(ex.Message);
        }

        var limits = await _limits.GetLimitsAsync(companyId, cancellationToken);
        var usage = await _db.TenantUsages.FirstOrDefaultAsync(cancellationToken);
        var used = usage?.StorageUsedMb ?? 0;
        var additionalMb = Math.Max(1, (long)Math.Ceiling(buffer.Length / (1024d * 1024d)));
        if (limits.MaxStorageMb >= 0 && used + additionalMb > limits.MaxStorageMb)
        {
            throw new PlanLimitExceededException("storage", used, limits.MaxStorageMb);
        }

        var id = Guid.NewGuid();
        var directory = Path.Combine(_environment.ContentRootPath, "App_Data", "company-files", companyId.ToString("N"));
        Directory.CreateDirectory(directory);
        var relative = Path.Combine("App_Data", "company-files", companyId.ToString("N"), id.ToString("N"));
        var fullPath = Path.Combine(_environment.ContentRootPath, relative);
        await File.WriteAllBytesAsync(fullPath, buffer.ToArray(), cancellationToken);

        var stored = new StoredFile
        {
            Id = id,
            TenantId = companyId,
            OwnerUserId = userId,
            FileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = buffer.Length,
            StoragePath = relative
        };
        _db.StoredFiles.Add(stored);
        if (usage is not null)
            usage.StorageUsedMb = used + additionalMb;
        await _db.SaveChangesAsync(cancellationToken);

        return new StoredFileDto
        {
            Id = stored.Id,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            CreatedAt = stored.CreatedAt
        };
    }

    public async Task<(Stream Content, StoredFileDto File)> OpenAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await _db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken)
            ?? throw new NotFoundException("File was not found.");

        var fullPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, file.StoragePath));
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "company-files"));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            throw new NotFoundException("File was not found.");

        return (System.IO.File.OpenRead(fullPath), new StoredFileDto
        {
            Id = file.Id,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.SizeBytes,
            CreatedAt = file.CreatedAt
        });
    }

    public async Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await _db.StoredFiles.FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken)
            ?? throw new NotFoundException("File was not found.");

        var fullPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, file.StoragePath));
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "App_Data", "company-files"));
        _db.StoredFiles.Remove(file);
        var usage = await _db.TenantUsages.FirstOrDefaultAsync(cancellationToken);
        if (usage is not null)
        {
            var mb = Math.Max(1, (long)Math.Ceiling(file.SizeBytes / (1024d * 1024d)));
            usage.StorageUsedMb = Math.Max(0, usage.StorageUsedMb - mb);
        }
        await _db.SaveChangesAsync(cancellationToken);
        if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }

    private Guid RequireCompany() =>
        _currentUser.TenantId ?? (_tenant.HasTenant() ? _tenant.TenantId : throw new BadRequestException("Company context is required."));
}

public sealed class PlatformAiOverride : IPlatformAiOverride
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ICacheService _cache;

    public PlatformAiOverride(IServiceScopeFactory scopes, ICacheService cache)
    {
        _scopes = scopes;
        _cache = cache;
    }

    public Task<string> ChatModelAsync(string fallback, CancellationToken cancellationToken = default) =>
        ReadAsync(PlatformSettingKeys.DefaultModel, fallback, cancellationToken);

    public Task<string> EmbeddingModelAsync(string fallback, CancellationToken cancellationToken = default) =>
        ReadAsync(PlatformSettingKeys.EmbeddingModel, fallback, cancellationToken);

    private async Task<string> ReadAsync(string key, string fallback, CancellationToken cancellationToken)
    {
        var cacheKey = "platform:ai:" + key;
        var cached = await _cache.GetAsync<string>(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
            return cached;

        await using var scope = _scopes.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BusinessOSDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var value = await db.PlatformSettings.AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);

        var resolved = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (!string.IsNullOrWhiteSpace(resolved))
            await _cache.SetAsync(cacheKey, resolved, TimeSpan.FromSeconds(30), cancellationToken: cancellationToken);
        return resolved;
    }
}

public static class CompanyOsSeeder
{
    public static async Task SeedAsync(BusinessOSDbContext context, CancellationToken cancellationToken = default)
    {
        var keys = await context.BusinessTypeTemplates.Select(x => x.Key).ToListAsync(cancellationToken);
        var known = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in BusinessTypeCatalog.Defaults)
        {
            if (known.Contains(definition.Key))
                continue;

            context.BusinessTypeTemplates.Add(new BusinessTypeTemplate
            {
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                ModulesJson = definition.ModulesJson,
                IsActive = true,
                SortOrder = definition.SortOrder
            });
        }

        var users = await context.Users
            .Select(x => new { x.Id, x.TenantId, x.JoinedAt })
            .ToListAsync(cancellationToken);
        var existing = await context.CompanyMemberships.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .Select(x => new { x.UserId, x.TenantId })
            .ToListAsync(cancellationToken);
        var pairs = existing.Select(x => x.UserId + ":" + x.TenantId).ToHashSet(StringComparer.Ordinal);

        var roleNames = await (
            from userRole in context.RbacUserRoles
            join role in context.RbacRoles on userRole.RoleId equals role.Id
            select new { userRole.UserId, role.Name }).ToListAsync(cancellationToken);
        var roleByUser = roleNames
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name).FirstOrDefault(name => BusinessOS.Application.Common.Authorization.RoleNames.All.Contains(name)));

        foreach (var user in users.Where(x => x.TenantId != Guid.Empty))
        {
            var pair = user.Id + ":" + user.TenantId;
            if (pairs.Contains(pair))
                continue;

            context.CompanyMemberships.Add(new CompanyMembership
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleName = roleByUser.GetValueOrDefault(user.Id) ?? BusinessOS.Application.Common.Authorization.RoleNames.Employee,
                IsActive = true,
                JoinedAt = user.JoinedAt == default ? DateTime.UtcNow : user.JoinedAt
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

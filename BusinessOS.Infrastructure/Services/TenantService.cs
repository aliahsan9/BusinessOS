using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Tenant.DTOs;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class TenantService : ITenantService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantLimitService _limitService;
    private readonly ITenantAuditService _auditService;

    public TenantService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITenantLimitService limitService,
        ITenantAuditService auditService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _limitService = limitService;
        _auditService = auditService;
    }

    public async Task<TenantDto> GetTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        return MapTenant(tenant);
    }

    public async Task<TenantDto> UpdateTenantAsync(
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        var oldValue = SerializeTenant(tenant);

        tenant.Name = request.Name.Trim();
        tenant.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        tenant.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone.Trim();
        tenant.Currency = request.Currency.Trim();
        tenant.BusinessType = request.BusinessType.Trim();
        tenant.Email = request.Email.Trim();
        tenant.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        tenant.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        tenant.UpdatedAt = DateTime.UtcNow;

        var settings = await GetOrCreateSettingsAsync(tenant.Id, cancellationToken);
        settings.LogoUrl = tenant.LogoUrl;
        settings.Currency = tenant.Currency;
        settings.Timezone = tenant.Timezone;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            tenant.Id,
            "TenantUpdated",
            "Tenant",
            oldValue,
            SerializeTenant(tenant),
            cancellationToken: cancellationToken);

        return MapTenant(tenant);
    }

    public async Task<TenantSettingsDto> GetTenantSettingsAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        var settings = await _context.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? new TenantSettings { Currency = tenant.Currency, Timezone = tenant.Timezone };

        return MapSettings(tenant, settings);
    }

    public async Task<TenantSettingsDto> UpdateTenantSettingsAsync(
        UpdateTenantSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        var settings = await GetOrCreateSettingsAsync(tenant.Id, cancellationToken);
        var oldValue = RbacAuditService.Serialize(new { tenant.Name, settings.Currency, settings.Timezone });

        tenant.Name = request.Name.Trim();
        tenant.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        tenant.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone.Trim();
        tenant.Currency = request.Currency.Trim();
        tenant.BusinessType = request.BusinessType.Trim();
        tenant.Email = request.Email.Trim();
        tenant.Phone = request.Phone.Trim();
        tenant.Address = request.Address.Trim();
        tenant.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        tenant.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        tenant.UpdatedAt = DateTime.UtcNow;

        settings.LogoUrl = tenant.LogoUrl;
        settings.Currency = tenant.Currency;
        settings.Timezone = tenant.Timezone;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            tenant.Id,
            "TenantSettingsUpdated",
            "TenantSettings",
            oldValue,
            RbacAuditService.Serialize(new { tenant.Name, settings.Currency, settings.Timezone }),
            cancellationToken: cancellationToken);

        return MapSettings(tenant, settings);
    }

    public async Task<TenantUsageDto> GetTenantUsageAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context is required.");

        await _limitService.RefreshUsageAsync(tenantId, cancellationToken);

        var tenant = await _context.Tenants
            .AsNoTracking()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");

        var usage = await _context.TenantUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? new TenantUsage();

        var limits = tenant.Plan is not null
            ? new Application.Common.Models.TenantLimits(
                tenant.Plan.MaxUsers,
                tenant.Plan.MaxCustomers,
                tenant.Plan.MaxProjects,
                tenant.Plan.MaxTasks,
                tenant.Plan.MaxStorageMb,
                tenant.Plan.MaxAiRequests)
            : new Application.Common.Models.TenantLimits(1, 25, 10, 100, 512, 0);

        return new TenantUsageDto
        {
            UserCount = usage.UserCount,
            MaxUsers = limits.MaxUsers,
            CustomerCount = usage.CustomerCount,
            MaxCustomers = limits.MaxCustomers,
            ProjectCount = usage.ProjectCount,
            MaxProjects = limits.MaxProjects,
            TaskCount = usage.TaskCount,
            MaxTasks = limits.MaxTasks,
            StorageUsedMb = usage.StorageUsedMb,
            MaxStorageMb = limits.MaxStorageMb,
            AiRequestsUsed = usage.AiRequestsUsed,
            MaxAiRequests = limits.MaxAiRequests,
            SubscriptionPlan = tenant.Plan?.Name ?? tenant.SubscriptionPlan,
            LastCalculatedAt = usage.LastCalculatedAt
        };
    }

    public async Task<TenantDashboardDto> GetTenantDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        var usage = await GetTenantUsageAsync(cancellationToken);

        return new TenantDashboardDto
        {
            Tenant = MapTenant(tenant),
            Usage = usage
        };
    }

    private async Task<Domain.Entities.Tenant> GetCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context is required.");

        return await _context.Tenants
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant not found.");
    }

    private async Task<TenantSettings> GetOrCreateSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var settings = await _context.TenantSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new TenantSettings
        {
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow
        };
        _context.TenantSettings.Add(settings);
        return settings;
    }

    private static TenantDto MapTenant(Domain.Entities.Tenant tenant) =>
        new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            LogoUrl = tenant.LogoUrl,
            Domain = tenant.Domain,
            Timezone = tenant.Timezone,
            Currency = tenant.Currency,
            Status = tenant.Status.ToString(),
            SubscriptionPlan = tenant.Plan?.Name ?? tenant.SubscriptionPlan,
            SubscriptionPlanId = tenant.SubscriptionPlanId,
            BusinessType = tenant.BusinessType,
            Email = tenant.Email,
            Website = tenant.Website,
            Description = tenant.Description,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        };

    private static TenantSettingsDto MapSettings(Domain.Entities.Tenant tenant, TenantSettings settings) =>
        new()
        {
            TenantId = tenant.Id,
            Name = tenant.Name,
            LogoUrl = tenant.LogoUrl ?? settings.LogoUrl,
            Timezone = tenant.Timezone,
            Currency = tenant.Currency,
            BusinessType = tenant.BusinessType,
            Email = tenant.Email,
            Phone = tenant.Phone,
            Address = tenant.Address,
            Website = tenant.Website,
            Description = tenant.Description,
            Theme = settings.Theme
        };

    private static string SerializeTenant(Domain.Entities.Tenant tenant) =>
        RbacAuditService.Serialize(new { tenant.Name, tenant.Slug, tenant.Status, tenant.SubscriptionPlan });
}

using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Organization.DTOs;
using BusinessOS.Application.Features.Organization.Services;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class OrganizationService : IOrganizationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRbacAuditService _auditService;

    public OrganizationService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IRbacAuditService auditService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    public async Task<OrganizationDto> GetOrganizationAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        var settings = await _context.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? new Domain.Entities.TenantSettings { Currency = "USD", Timezone = "UTC" };

        return MapOrganization(tenant, settings);
    }

    public async Task<OrganizationDto> UpdateOrganizationAsync(
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        var settings = await _context.TenantSettings.FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            settings = new Domain.Entities.TenantSettings
            {
                TenantId = tenant.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.TenantSettings.Add(settings);
        }

        var oldValue = RbacAuditService.Serialize(new { tenant.Name, settings.Currency, settings.Timezone });

        tenant.Name = request.Name.Trim();
        tenant.BusinessType = request.BusinessType.Trim();
        tenant.Email = request.Email.Trim();
        tenant.Phone = request.Phone.Trim();
        tenant.Address = request.Address.Trim();
        tenant.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        tenant.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        tenant.UpdatedAt = DateTime.UtcNow;

        settings.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        settings.Currency = request.Currency.Trim();
        settings.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone.Trim();
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "OrganizationUpdated",
            ActivityEntityTypes.Settings,
            tenant.Id.ToString(),
            oldValue,
            RbacAuditService.Serialize(new { tenant.Name, settings.Currency, settings.Timezone }),
            cancellationToken);

        return MapOrganization(tenant, settings);
    }

    private async Task<Domain.Entities.Tenant> GetCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context is required.");

        return await _context.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Organization not found.");
    }

    private static OrganizationDto MapOrganization(
        Domain.Entities.Tenant tenant,
        Domain.Entities.TenantSettings settings) =>
        new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            BusinessType = tenant.BusinessType,
            Email = tenant.Email,
            Phone = tenant.Phone,
            Address = tenant.Address,
            Website = tenant.Website,
            Description = tenant.Description,
            LogoUrl = settings.LogoUrl,
            Currency = settings.Currency,
            Timezone = settings.Timezone,
            SubscriptionPlan = tenant.SubscriptionPlan,
            IsActive = tenant.IsActive
        };
}

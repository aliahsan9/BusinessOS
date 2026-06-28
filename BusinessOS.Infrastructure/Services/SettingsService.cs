using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Settings.DTOs;
using BusinessOS.Application.Features.Settings.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SettingsService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<TenantSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateTenantSettingsAsync(cancellationToken);
        return MapSettings(settings);
    }

    public async Task<TenantSettingsDto> UpdateSettingsAsync(
        UpdateTenantSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateTenantSettingsAsync(cancellationToken);

        settings.Currency = request.Currency.Trim();
        settings.Language = request.Language.Trim();
        settings.TaxRate = Math.Round(request.TaxRate, 2);
        settings.InvoicePrefix = string.IsNullOrWhiteSpace(request.InvoicePrefix)
            ? null
            : request.InvoicePrefix.Trim();
        settings.EmailFromAddress = string.IsNullOrWhiteSpace(request.EmailFromAddress)
            ? null
            : request.EmailFromAddress.Trim();
        settings.Theme = string.IsNullOrWhiteSpace(request.Theme) ? "light" : request.Theme.Trim();
        settings.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        settings.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
        settings.SystemNotificationsEnabled = request.SystemNotificationsEnabled;
        settings.OrderNotificationsEnabled = request.OrderNotificationsEnabled;
        settings.InventoryAlertsEnabled = request.InventoryAlertsEnabled;
        settings.PaymentAlertsEnabled = request.PaymentAlertsEnabled;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapSettings(settings);
    }

    public async Task<BusinessProfileDto> GetBusinessProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        var settings = await GetOrCreateTenantSettingsAsync(cancellationToken);

        return MapBusinessProfile(tenant, settings);
    }

    public async Task<BusinessProfileDto> UpdateBusinessProfileAsync(
        UpdateBusinessProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        tenant.Name = request.Name.Trim();
        tenant.BusinessType = request.BusinessType.Trim();
        tenant.Email = request.Email.Trim();
        tenant.Phone = request.Phone.Trim();
        tenant.Address = request.Address.Trim();
        tenant.UpdatedAt = DateTime.UtcNow;

        var settings = await GetOrCreateTenantSettingsAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MapBusinessProfile(tenant, settings);
    }

    private async Task<Tenant> GetCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context is required.");

        return await _context.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");
    }

    private async Task<TenantSettings> GetOrCreateTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context is required.");

        var settings = await _context.TenantSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (settings is not null)
            return settings;

        settings = new TenantSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId
        };

        _context.TenantSettings.Add(settings);
        await _context.SaveChangesAsync(cancellationToken);

        return settings;
    }

    private static TenantSettingsDto MapSettings(TenantSettings settings) =>
        new()
        {
            Id = settings.Id,
            TenantId = settings.TenantId,
            Currency = settings.Currency,
            Language = settings.Language,
            TaxRate = settings.TaxRate,
            InvoicePrefix = settings.InvoicePrefix,
            EmailFromAddress = settings.EmailFromAddress,
            Theme = settings.Theme,
            LogoUrl = settings.LogoUrl,
            EmailNotificationsEnabled = settings.EmailNotificationsEnabled,
            SystemNotificationsEnabled = settings.SystemNotificationsEnabled,
            OrderNotificationsEnabled = settings.OrderNotificationsEnabled,
            InventoryAlertsEnabled = settings.InventoryAlertsEnabled,
            PaymentAlertsEnabled = settings.PaymentAlertsEnabled
        };

    private static BusinessProfileDto MapBusinessProfile(Tenant tenant, TenantSettings settings) =>
        new()
        {
            TenantId = tenant.Id,
            Name = tenant.Name,
            BusinessType = tenant.BusinessType,
            Email = tenant.Email,
            Phone = tenant.Phone,
            Address = tenant.Address,
            SubscriptionPlan = tenant.SubscriptionPlan,
            IsActive = tenant.IsActive,
            Settings = MapSettings(settings)
        };
}

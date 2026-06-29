using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Application.Features.Settings.DTOs;
using BusinessOS.Application.Features.Settings.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBusinessEventService _businessEvents;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IBusinessEventService businessEvents,
        ILogger<SettingsService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _businessEvents = businessEvents;
        _logger = logger;
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
        settings.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone.Trim();
        settings.AiAssistantEnabled = request.AiAssistantEnabled;
        settings.AiShowSuggestions = request.AiShowSuggestions;
        settings.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
        settings.SystemNotificationsEnabled = request.SystemNotificationsEnabled;
        settings.OrderNotificationsEnabled = request.OrderNotificationsEnabled;
        settings.InventoryAlertsEnabled = request.InventoryAlertsEnabled;
        settings.PaymentAlertsEnabled = request.PaymentAlertsEnabled;
        settings.TaskNotificationsEnabled = request.TaskNotificationsEnabled;
        settings.InvoiceNotificationsEnabled = request.InvoiceNotificationsEnabled;
        settings.CustomerNotificationsEnabled = request.CustomerNotificationsEnabled;
        settings.ProjectNotificationsEnabled = request.ProjectNotificationsEnabled;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await PublishSettingsUpdatedAsync(settings.Id, cancellationToken);

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
        tenant.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        tenant.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        tenant.UpdatedAt = DateTime.UtcNow;

        var settings = await GetOrCreateTenantSettingsAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await PublishSettingsUpdatedAsync(settings.Id, cancellationToken);

        return MapBusinessProfile(tenant, settings);
    }

    private async Task PublishSettingsUpdatedAsync(Guid settingsId, CancellationToken cancellationToken)
    {
        try
        {
            await _businessEvents.PublishAsync(
                new BusinessEventRequest(
                    ActivityActions.Updated,
                    ActivityEntityTypes.Settings,
                    settingsId,
                    "Business Settings",
                    "Business Settings Updated",
                    "Business settings were updated.",
                    NotificationTypes.System),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish settings updated event");
        }
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
            Timezone = settings.Timezone,
            AiAssistantEnabled = settings.AiAssistantEnabled,
            AiShowSuggestions = settings.AiShowSuggestions,
            EmailNotificationsEnabled = settings.EmailNotificationsEnabled,
            SystemNotificationsEnabled = settings.SystemNotificationsEnabled,
            OrderNotificationsEnabled = settings.OrderNotificationsEnabled,
            InventoryAlertsEnabled = settings.InventoryAlertsEnabled,
            PaymentAlertsEnabled = settings.PaymentAlertsEnabled,
            TaskNotificationsEnabled = settings.TaskNotificationsEnabled,
            InvoiceNotificationsEnabled = settings.InvoiceNotificationsEnabled,
            CustomerNotificationsEnabled = settings.CustomerNotificationsEnabled,
            ProjectNotificationsEnabled = settings.ProjectNotificationsEnabled
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
            Website = tenant.Website,
            Description = tenant.Description,
            Settings = MapSettings(settings)
        };
}

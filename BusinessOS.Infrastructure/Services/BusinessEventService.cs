using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Activities.Services;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class BusinessEventService : IBusinessEventService
{
    private readonly IActivityService _activityService;
    private readonly INotificationService _notificationService;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<BusinessEventService> _logger;

    public BusinessEventService(
        IActivityService activityService,
        INotificationService notificationService,
        IEmailNotificationService emailNotificationService,
        IRealtimeNotificationService realtimeNotificationService,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<BusinessEventService> logger)
    {
        _activityService = activityService;
        _notificationService = notificationService;
        _emailNotificationService = emailNotificationService;
        _realtimeNotificationService = realtimeNotificationService;
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task PublishAsync(
        BusinessEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new Application.Common.Exceptions.BadRequestException("Tenant context is required.");

        var userId = _currentUserService.UserId
            ?? throw new Application.Common.Exceptions.UnauthorizedException("User context is required.");

        var activity = await _activityService.LogAsync(
            new LogActivityRequest(
                request.Action,
                request.EntityType,
                request.EntityId,
                request.EntityName,
                request.Metadata),
            cancellationToken);

        await _realtimeNotificationService.PushActivityAsync(tenantId, activity, cancellationToken);

        if (!await ShouldNotifyAsync(request.EntityType, cancellationToken))
            return;

        var notification = await _notificationService.CreateForCurrentUserAsync(
            request.NotificationTitle,
            request.NotificationMessage,
            request.NotificationType,
            request.Link,
            cancellationToken);

        await _realtimeNotificationService.PushNotificationAsync(
            tenantId,
            userId,
            notification,
            cancellationToken);

        var unreadCount = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
        await _realtimeNotificationService.PushUnreadCountAsync(userId, unreadCount, cancellationToken);

        await TrySendEmailAsync(request, cancellationToken);
    }

    private async Task<bool> ShouldNotifyAsync(
        string entityType,
        CancellationToken cancellationToken)
    {
        var settings = await _context.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
            return true;

        return entityType switch
        {
            ActivityEntityTypes.Customer => settings.CustomerNotificationsEnabled,
            ActivityEntityTypes.Project => settings.ProjectNotificationsEnabled,
            ActivityEntityTypes.Task => settings.TaskNotificationsEnabled,
            ActivityEntityTypes.Invoice => settings.InvoiceNotificationsEnabled,
            ActivityEntityTypes.Expense => settings.PaymentAlertsEnabled,
            ActivityEntityTypes.Settings => settings.SystemNotificationsEnabled,
            _ => settings.SystemNotificationsEnabled
        };
    }

    private async Task TrySendEmailAsync(
        BusinessEventRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _context.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null || !settings.EmailNotificationsEnabled)
            return;

        var email = _currentUserService.Email;
        if (string.IsNullOrWhiteSpace(email))
            return;

        try
        {
            await _emailNotificationService.SendAsync(
                email,
                request.NotificationTitle,
                request.NotificationMessage,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to queue email notification for {Email}", email);
        }
    }
}

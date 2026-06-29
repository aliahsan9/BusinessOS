using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public NotificationService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<NotificationResponse>> GetForUserAsync(
        string userId,
        bool? unreadOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationParams.Normalize(page, pageSize);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (unreadOnly == true)
            query = query.Where(x => !x.IsRead);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => MapNotification(x))
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationResponse>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<int> GetUnreadCountAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await _context.Notifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

    public async Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId, cancellationToken)
            ?? throw new NotFoundException($"Notification '{notificationId}' was not found.");

        notification.IsRead = true;
        notification.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _context.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId, cancellationToken)
            ?? throw new NotFoundException($"Notification '{notificationId}' was not found.");

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationPreferencesDto> GetPreferencesAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateTenantSettingsAsync(cancellationToken);
        return MapPreferences(settings);
    }

    public async Task<NotificationPreferencesDto> UpdatePreferencesAsync(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateTenantSettingsAsync(cancellationToken);

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

        return MapPreferences(settings);
    }

    public async Task<NotificationResponse> CreateNotificationAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BadRequestException("Title is required.");

        if (string.IsNullOrWhiteSpace(request.Message))
            throw new BadRequestException("Message is required.");

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Info" : request.Type.Trim(),
            IsRead = false,
            Link = string.IsNullOrWhiteSpace(request.Link) ? null : request.Link.Trim(),
            CreatedBy = ResolveUserName()
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return MapNotification(notification);
    }

    public async Task<NotificationResponse> CreateForCurrentUserAsync(
        string title,
        string message,
        string type,
        string? link = null,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedException("User context is required.");

        return await CreateNotificationAsync(
            new CreateNotificationRequest(userId, title, message, type, link),
            cancellationToken);
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

    private string ResolveUserName()
    {
        var email = _currentUserService.Email;
        if (string.IsNullOrWhiteSpace(email))
            return "System";

        return email.Split('@')[0];
    }

    private static NotificationResponse MapNotification(Notification notification) =>
        new()
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type,
            IsRead = notification.IsRead,
            Link = notification.Link,
            CreatedAt = notification.CreatedAt,
            CreatedBy = notification.CreatedBy
        };

    private static NotificationPreferencesDto MapPreferences(TenantSettings settings) =>
        new()
        {
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
}

using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BusinessOS.API.Services;

public sealed class SignalRRealtimeNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    public SignalRRealtimeNotificationService(
        IHubContext<NotificationHub, INotificationHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PushNotificationAsync(
        Guid tenantId,
        string userId,
        NotificationResponse notification,
        CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients
            .Group(TenantGroup(tenantId))
            .ReceiveNotification(notification);

        await _hubContext.Clients
            .User(userId)
            .ReceiveNotification(notification);
    }

    public Task PushActivityAsync(
        Guid tenantId,
        ActivityResponse activity,
        CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(TenantGroup(tenantId))
            .ReceiveActivity(activity);

    public Task PushUnreadCountAsync(
        string userId,
        int unreadCount,
        CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .User(userId)
            .UnreadCountUpdated(unreadCount);

    private static string TenantGroup(Guid tenantId) => $"tenant-{tenantId}";
}

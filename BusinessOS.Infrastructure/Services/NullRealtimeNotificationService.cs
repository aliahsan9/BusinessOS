using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;

namespace BusinessOS.Infrastructure.Services;

public sealed class NullRealtimeNotificationService : IRealtimeNotificationService
{
    public Task PushNotificationAsync(
        Guid tenantId,
        string userId,
        NotificationResponse notification,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PushActivityAsync(
        Guid tenantId,
        ActivityResponse activity,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task PushUnreadCountAsync(
        string userId,
        int unreadCount,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

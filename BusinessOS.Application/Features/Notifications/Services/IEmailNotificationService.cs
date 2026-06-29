using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.DTOs;

namespace BusinessOS.Application.Features.Notifications.Services;

public interface IEmailNotificationService
{
    Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}

public interface IRealtimeNotificationService
{
    Task PushNotificationAsync(
        Guid tenantId,
        string userId,
        NotificationResponse notification,
        CancellationToken cancellationToken = default);

    Task PushActivityAsync(
        Guid tenantId,
        ActivityResponse activity,
        CancellationToken cancellationToken = default);

    Task PushUnreadCountAsync(
        string userId,
        int unreadCount,
        CancellationToken cancellationToken = default);
}

public interface IBusinessEventService
{
    Task PublishAsync(
        BusinessEventRequest request,
        CancellationToken cancellationToken = default);
}

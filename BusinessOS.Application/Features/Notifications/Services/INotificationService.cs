using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Notifications.DTOs;

namespace BusinessOS.Application.Features.Notifications.Services;

public interface INotificationService
{
    Task<PagedResult<NotificationResponse>> GetForUserAsync(
        string userId,
        bool? unreadOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(string userId, CancellationToken cancellationToken = default);

    Task<NotificationPreferencesDto> GetPreferencesAsync(
        CancellationToken cancellationToken = default);

    Task<NotificationPreferencesDto> UpdatePreferencesAsync(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationResponse> CreateNotificationAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);
}

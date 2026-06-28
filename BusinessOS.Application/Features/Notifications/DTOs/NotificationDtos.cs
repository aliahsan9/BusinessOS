namespace BusinessOS.Application.Features.Notifications.DTOs;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string Type { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationPreferencesDto
{
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool SystemNotificationsEnabled { get; set; } = true;
    public bool OrderNotificationsEnabled { get; set; } = true;
    public bool InventoryAlertsEnabled { get; set; } = true;
    public bool PaymentAlertsEnabled { get; set; } = true;
}

public record UpdateNotificationPreferencesRequest(
    bool EmailNotificationsEnabled,
    bool SystemNotificationsEnabled,
    bool OrderNotificationsEnabled,
    bool InventoryAlertsEnabled,
    bool PaymentAlertsEnabled);

public record CreateNotificationRequest(
    string UserId,
    string Title,
    string Message,
    string Type);

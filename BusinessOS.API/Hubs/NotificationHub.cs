using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Notifications.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace BusinessOS.API.Hubs;

public interface INotificationHubClient
{
    Task ReceiveNotification(NotificationResponse notification);
    Task ReceiveActivity(ActivityResponse activity);
    Task UnreadCountUpdated(int unreadCount);
}

public sealed class NotificationHub : Hub<INotificationHubClient>
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("TenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
        }

        await base.OnConnectedAsync();
    }
}

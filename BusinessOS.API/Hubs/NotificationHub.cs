using System.Security.Claims;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
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
    private readonly ITenantMembershipLookup _membershipLookup;

    public NotificationHub(ITenantMembershipLookup membershipLookup)
    {
        _membershipLookup = membershipLookup;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var membership = string.IsNullOrWhiteSpace(userId)
            ? null
            : await _membershipLookup.FindByUserIdAsync(userId, Context.ConnectionAborted);

        var versionClaim = Context.User?.FindFirst(ClaimTypesConstants.SecurityVersion)?.Value;
        var versionMatches = membership is not null &&
                             int.TryParse(versionClaim, out var tokenVersion) &&
                             tokenVersion == membership.SecurityVersion;

        if (membership is null ||
            !membership.IsActive ||
            !versionMatches ||
            (membership.LockoutEnd is DateTimeOffset lockoutEnd && lockoutEnd > DateTimeOffset.UtcNow))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{membership.TenantId}");
        await base.OnConnectedAsync();
    }

    public Task JoinTenant(string tenantId) =>
        throw new HubException("Clients cannot choose a tenant group.");
}

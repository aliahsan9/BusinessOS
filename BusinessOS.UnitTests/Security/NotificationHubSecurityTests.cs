using BusinessOS.API.Hubs;
using BusinessOS.Application.Common.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace BusinessOS.UnitTests.Security;

public class NotificationHubSecurityTests
{
    [Fact]
    public async Task JoinTenant_RejectsAClientSelectedTenant()
    {
        var hub = new NotificationHub(Mock.Of<ITenantMembershipLookup>());

        var act = () => hub.JoinTenant(Guid.NewGuid().ToString());

        await act.Should().ThrowAsync<HubException>();
    }
}

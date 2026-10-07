using System.Net;
using System.Net.Http.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Roles.DTOs;
using BusinessOS.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class AuthorizationIntegrationTests : IntegrationTestBase
{
    public AuthorizationIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_ReturnsUnauthorized()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/products");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                "invalid.token.value");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ViewerUser_CannotCreateCategory_ReturnsForbidden()
    {
        // Registration creates the tenant Owner.
        var ownerAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        // Convert Owner -> Viewer.
        await SwitchToViewerRoleAsync(ownerAuth);

        // Login again so the JWT contains the Viewer role/permissions.
        var viewerAuth = await LoginAsync(ownerAuth.Email);

        // Verify that the test really has the expected role.
        viewerAuth.Roles.Should().Contain(RoleNames.Viewer);
        viewerAuth.Roles.Should().NotContain(RoleNames.Owner);

        // Viewer has Category.View but NOT Category.Create.
        viewerAuth.Permissions.Should().Contain(PermissionCodes.CategoryView);
        viewerAuth.Permissions.Should().NotContain(PermissionCodes.CategoryCreate);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            viewerAuth,
            new
            {
                name = $"Blocked Category {Guid.NewGuid():N}"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ViewerUser_CannotDeleteOrder_ReturnsForbidden()
    {
        // Registration creates the tenant Owner.
        var ownerAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        // Convert Owner -> Viewer.
        await SwitchToViewerRoleAsync(ownerAuth);

        // Login again so the JWT contains Viewer permissions.
        var viewerAuth = await LoginAsync(ownerAuth.Email);

        viewerAuth.Roles.Should().Contain(RoleNames.Viewer);
        viewerAuth.Roles.Should().NotContain(RoleNames.Owner);

        // Viewer has Order.View but NOT Order.Delete.
        viewerAuth.Permissions.Should().Contain(PermissionCodes.OrderView);
        viewerAuth.Permissions.Should().NotContain(PermissionCodes.OrderDelete);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/orders/{Guid.NewGuid()}",
            viewerAuth);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task SwitchToViewerRoleAsync(AuthResponse ownerAuth)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<BusinessOSDbContext>();

        // Registration creates an Owner, NOT an Admin.
        var ownerRoleId = context.RbacRoles
            .Single(x => x.Name == RoleNames.Owner)
            .Id;

        var viewerRoleId = context.RbacRoles
            .Single(x => x.Name == RoleNames.Viewer)
            .Id;

        // Remove Owner role.
        var removeOwnerResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/users/{ownerAuth.UserId}/roles/{ownerRoleId}",
            ownerAuth);

        removeOwnerResponse.IsSuccessStatusCode.Should().BeTrue();

        // Assign Viewer role.
        var assignViewerResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/users/{ownerAuth.UserId}/roles",
            ownerAuth,
            new AssignUserRoleRequest(viewerRoleId));

        assignViewerResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    private async Task<AuthResponse> LoginAsync(string email)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password = "Password1!"
            });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}

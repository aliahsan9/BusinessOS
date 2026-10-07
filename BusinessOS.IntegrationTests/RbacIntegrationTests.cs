using System.Net;
using System.Net.Http.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Roles.DTOs;
using BusinessOS.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class RbacIntegrationTests : IntegrationTestBase
{
    public RbacIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task OwnerUser_CanAccessRolesAndPermissions()
    {
        // Registration creates the tenant owner.
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        auth.Roles.Should().Contain(RoleNames.Owner);

        var rolesResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/roles",
            auth);

        rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var permissionsResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/permissions",
            auth);

        permissionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var permissions =
            await permissionsResponse.Content.ReadFromJsonAsync<List<PermissionDto>>();

        permissions.Should().NotBeNull();
        permissions!.Should().Contain(x => x.Code == PermissionCodes.ProductView);
    }

    [Fact]
    public async Task AdminUser_CanAccessRolesAndPermissions()
    {
        // Registration creates the Owner.
        var ownerAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        // Get Admin role.
        var adminRoleId = await GetRoleIdAsync(RoleNames.Admin);

        // Remove Owner role.
        var removeOwnerResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/users/{ownerAuth.UserId}/roles/{await GetRoleIdAsync(RoleNames.Owner)}",
            ownerAuth);

        removeOwnerResponse.IsSuccessStatusCode.Should().BeTrue();

        // Assign Admin role.
        var assignAdminResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/users/{ownerAuth.UserId}/roles",
            ownerAuth,
            new AssignUserRoleRequest(adminRoleId));

        assignAdminResponse.IsSuccessStatusCode.Should().BeTrue();

        // Login again so the JWT contains the new role/permissions.
        var adminAuth = await LoginAsync(ownerAuth.Email);

        adminAuth.Roles.Should().Contain(RoleNames.Admin);
        adminAuth.Permissions.Should().Contain(PermissionCodes.RoleView);

        var rolesResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/roles",
            adminAuth);

        rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var permissionsResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/permissions",
            adminAuth);

        permissionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ViewerUser_IsDeniedProductCreate()
    {
        // Registration creates the Owner.
        var ownerAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var ownerRoleId = await GetRoleIdAsync(RoleNames.Owner);
        var viewerRoleId = await GetRoleIdAsync(RoleNames.Viewer);

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

        // Login again so the JWT contains Viewer permissions.
        var viewerAuth = await LoginAsync(ownerAuth.Email);

        viewerAuth.Roles.Should().Contain(RoleNames.Viewer);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/products",
            viewerAuth,
            new
            {
                name = "Blocked Product",
                sku = $"SKU-{Guid.NewGuid():N}",
                price = 10m,
                categoryId = Guid.NewGuid()
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ViewerUser_CanViewProducts()
    {
        // Registration creates the Owner.
        var ownerAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var ownerRoleId = await GetRoleIdAsync(RoleNames.Owner);
        var viewerRoleId = await GetRoleIdAsync(RoleNames.Viewer);

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

        // Login again so the JWT contains Viewer permissions.
        var viewerAuth = await LoginAsync(ownerAuth.Email);

        viewerAuth.Roles.Should().Contain(RoleNames.Viewer);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/products?page=1&pageSize=10",
            viewerAuth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OwnerJwt_ContainsPermissionClaim()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        auth.Roles.Should().Contain(RoleNames.Owner);
        auth.Permissions.Should().Contain(PermissionCodes.RoleView);
    }

    private Task<Guid> GetRoleIdAsync(string roleName)
    {
        using var scope = Factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<BusinessOSDbContext>();

        var role = context.RbacRoles.Single(x => x.Name == roleName);

        return Task.FromResult(role.Id);
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

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
    public async Task AdminUser_CanAccessRolesAndPermissions()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var rolesResponse = await IntegrationHttp.SendAuthorizedAsync(Client, HttpMethod.Get, "/api/roles", auth);
        rolesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var permissionsResponse = await IntegrationHttp.SendAuthorizedAsync(Client, HttpMethod.Get, "/api/permissions", auth);
        permissionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var permissions = await permissionsResponse.Content.ReadFromJsonAsync<List<PermissionDto>>();
        permissions.Should().NotBeNull();
        permissions!.Should().Contain(x => x.Code == PermissionCodes.ProductView);
    }

    [Fact]
    public async Task ViewerUser_IsDeniedProductCreate()
    {
        var adminAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var adminRoleId = await GetRoleIdAsync(RoleNames.Admin);
        var viewerRoleId = await GetRoleIdAsync(RoleNames.Viewer);

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/users/{adminAuth.UserId}/roles/{adminRoleId}",
            adminAuth);

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/users/{adminAuth.UserId}/roles",
            adminAuth,
            new AssignUserRoleRequest(viewerRoleId));

        var viewerAuth = await LoginAsync(adminAuth.Email);

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
        var adminAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var adminRoleId = await GetRoleIdAsync(RoleNames.Admin);
        var viewerRoleId = await GetRoleIdAsync(RoleNames.Viewer);

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/users/{adminAuth.UserId}/roles/{adminRoleId}",
            adminAuth);

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/users/{adminAuth.UserId}/roles",
            adminAuth,
            new AssignUserRoleRequest(viewerRoleId));

        var viewerAuth = await LoginAsync(adminAuth.Email);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/products?page=1&pageSize=10",
            viewerAuth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminJwt_ContainsPermissionClaim()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        auth.Permissions.Should().Contain(PermissionCodes.RoleView);
        auth.Roles.Should().Contain(RoleNames.Admin);
    }

    private Task<Guid> GetRoleIdAsync(string roleName)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var role = context.RbacRoles.Single(x => x.Name == roleName);
        return Task.FromResult(role.Id);
    }

    private async Task<AuthResponse> LoginAsync(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password1!"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}

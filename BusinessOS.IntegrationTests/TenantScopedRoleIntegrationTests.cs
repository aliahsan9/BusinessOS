using System.Net;
using System.Net.Http.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Roles.DTOs;
using BusinessOS.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class TenantScopedRoleIntegrationTests : IntegrationTestBase
{
    public TenantScopedRoleIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CompanyA_CannotViewCompanyB_CustomRoles()
    {
        var companyA = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var companyB = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var createResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/roles",
            companyB,
            new CreateRoleRequest("CompanyB Auditor", "B-only role"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<RoleDto>();
        created.Should().NotBeNull();
        created!.IsSystem.Should().BeFalse();
        created.TenantId.Should().Be(companyB.TenantId);

        var listA = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/roles", companyA);
        listA.StatusCode.Should().Be(HttpStatusCode.OK);
        var rolesA = await listA.Content.ReadFromJsonAsync<List<RoleDto>>();
        rolesA.Should().NotBeNull();
        rolesA!.Should().NotContain(x => x.Id == created.Id);
        rolesA.Should().NotContain(x => x.Name == "CompanyB Auditor");
        rolesA.Should().Contain(x => x.IsSystem && x.Name == RoleNames.Owner);

        var getA = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/roles/{created.Id}", companyA);
        getA.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompanyA_CannotEditOrDeleteCompanyB_CustomRoles()
    {
        var companyA = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var companyB = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var createResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/roles",
            companyB,
            new CreateRoleRequest("CompanyB Editor", "B-only"));

        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<RoleDto>())!;

        var editA = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            $"/api/v1/roles/{created.Id}",
            companyA,
            new UpdateRoleRequest("Hijacked", "stolen", true));
        editA.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteA = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Delete, $"/api/v1/roles/{created.Id}", companyA);
        deleteA.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var stillThere = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/roles/{created.Id}", companyB);
        stillThere.StatusCode.Should().Be(HttpStatusCode.OK);
        var role = await stillThere.Content.ReadFromJsonAsync<RoleDto>();
        role!.Name.Should().Be("CompanyB Editor");
    }

    [Fact]
    public async Task CompanyA_CannotAssignPermissions_ToCompanyB_Roles()
    {
        var companyA = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var companyB = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var createResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/roles",
            companyB,
            new CreateRoleRequest("CompanyB Ops", "B-only"));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<RoleDto>())!;

        var permissionId = await GetPermissionIdAsync(PermissionCodes.ProductView);

        var assignA = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/roles/{created.Id}/permissions",
            companyA,
            new AssignPermissionRequest(permissionId));
        assignA.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var assignB = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/roles/{created.Id}/permissions",
            companyB,
            new AssignPermissionRequest(permissionId));
        assignB.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var roleB = await (await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/roles/{created.Id}", companyB))
            .Content.ReadFromJsonAsync<RoleDto>();
        roleB!.Permissions.Should().Contain(PermissionCodes.ProductView);
    }

    [Fact]
    public async Task SystemRoles_RemainSharedAndImmutable()
    {
        var companyA = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var companyB = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var rolesA = await (await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/roles", companyA))
            .Content.ReadFromJsonAsync<List<RoleDto>>();
        var rolesB = await (await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/roles", companyB))
            .Content.ReadFromJsonAsync<List<RoleDto>>();

        var managerA = rolesA!.Single(x => x.Name == RoleNames.Manager);
        var managerB = rolesB!.Single(x => x.Name == RoleNames.Manager);
        managerA.Id.Should().Be(managerB.Id);
        managerA.IsSystem.Should().BeTrue();
        managerA.TenantId.Should().BeNull();

        var edit = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            $"/api/v1/roles/{managerA.Id}",
            companyA,
            new UpdateRoleRequest("ManagerX", "nope", true));
        edit.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var delete = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Delete, $"/api/v1/roles/{managerA.Id}", companyA);
        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var permissionId = await GetPermissionIdAsync(PermissionCodes.ProductCreate);
        var assign = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/roles/{managerA.Id}/permissions",
            companyA,
            new AssignPermissionRequest(permissionId));
        assign.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var reserved = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/roles",
            companyA,
            new CreateRoleRequest(RoleNames.Manager, "shadow"));
        reserved.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExistingLegitimateRoleAssignments_ContinueWorking()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        owner.Roles.Should().Contain(RoleNames.Owner);
        owner.Permissions.Should().Contain(PermissionCodes.RoleView);
        owner.Permissions.Should().Contain(PermissionCodes.ProductCreate);

        var createCustom = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/roles",
            owner,
            new CreateRoleRequest("Warehouse Lead", "Custom warehouse role"));
        createCustom.StatusCode.Should().Be(HttpStatusCode.Created);
        var custom = (await createCustom.Content.ReadFromJsonAsync<RoleDto>())!;

        var permissionId = await GetPermissionIdAsync(PermissionCodes.InventoryView);
        var assignPermission = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/roles/{custom.Id}/permissions",
            owner,
            new AssignPermissionRequest(permissionId));
        assignPermission.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var assignUser = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/users/{owner.UserId}/roles",
            owner,
            new AssignUserRoleRequest(custom.Id));
        assignUser.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var assignment = await db.RbacUserRoles
            .AsNoTracking()
            .SingleAsync(x => x.UserId == owner.UserId && x.RoleId == custom.Id);
        assignment.TenantId.Should().Be(owner.TenantId);

        var refreshed = await LoginAsync(owner.Email);
        refreshed.Roles.Should().Contain(RoleNames.Owner);
        refreshed.Roles.Should().Contain("Warehouse Lead");
        refreshed.Permissions.Should().Contain(PermissionCodes.InventoryView);
        refreshed.Permissions.Should().Contain(PermissionCodes.ProductCreate);
    }

    private Task<Guid> GetPermissionIdAsync(string code)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var permission = context.Permissions.Single(x => x.Code == code);
        return Task.FromResult(permission.Id);
    }

    private async Task<AuthResponse> LoginAsync(string email)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = "Password1!" });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}

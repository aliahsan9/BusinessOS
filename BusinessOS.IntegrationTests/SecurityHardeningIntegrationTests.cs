using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Roles.DTOs;
using BusinessOS.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class SecurityHardeningIntegrationTests : IntegrationTestBase
{
    public SecurityHardeningIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData("/api/v1/products")]
    [InlineData("/api/v1/orders")]
    [InlineData("/api/v1/customers")]
    [InlineData("/api/v1/finance/dashboard")]
    [InlineData("/api/v1/ai/conversations")]
    public async Task UnauthenticatedRequest_IsRejected(string url)
    {
        var response = await Client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmployeeRole_IsDeniedProductCreateAndFinance()
    {
        var ownerAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var ownerRoleId = await GetRoleIdAsync(RoleNames.Owner);
        var employeeRoleId = await GetRoleIdAsync(RoleNames.Employee);

        var removeOwner = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/v1/users/{ownerAuth.UserId}/roles/{ownerRoleId}",
            ownerAuth);
        removeOwner.IsSuccessStatusCode.Should().BeTrue();

        var assignEmployee = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/users/{ownerAuth.UserId}/roles",
            ownerAuth,
            new AssignUserRoleRequest(employeeRoleId));
        assignEmployee.IsSuccessStatusCode.Should().BeTrue();

        var employeeAuth = await LoginAsync(ownerAuth.Email);
        employeeAuth.Roles.Should().Contain(RoleNames.Employee);

        var createProduct = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/products",
            employeeAuth,
            new
            {
                categoryId = Guid.NewGuid(),
                name = "Blocked",
                sku = "NOPE",
                costPrice = 1,
                salePrice = 2,
                reorderLevel = 0
            });
        createProduct.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var finance = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/finance/dashboard",
            employeeAuth);
        finance.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CrossTenant_ReadUpdateAndDelete_AreRejected()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var intruder = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var customerId = await CreateCustomerAsync(owner);
        var productId = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, owner, 5);
        var orderId = await CreateOrderAsync(owner, customerId, productId);

        var productRead = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/products/{productId}", intruder);
        productRead.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var productUpdate = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            $"/api/v1/products/{productId}",
            intruder,
            new
            {
                categoryId = Guid.NewGuid(),
                name = "Stolen",
                sku = "STOLEN",
                description = "nope",
                costPrice = 1,
                salePrice = 2,
                reorderLevel = 1,
                isActive = true
            });
        productUpdate.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var productDelete = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Delete, $"/api/v1/products/{productId}", intruder);
        productDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var customerRead = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/customers/{customerId}", intruder);
        customerRead.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var customerUpdate = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            $"/api/v1/customers/{customerId}",
            intruder,
            new
            {
                firstName = "Stolen",
                lastName = "Customer",
                email = $"stolen_{Guid.NewGuid():N}@test.com",
                phoneNumber = "1234567890",
                address = "1 Other St",
                city = "Lahore",
                country = "Pakistan",
                postalCode = "54000",
                isActive = true
            });
        customerUpdate.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var customerDelete = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Delete, $"/api/v1/customers/{customerId}", intruder);
        customerDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var orderRead = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/orders/{orderId}", intruder);
        orderRead.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var orderUpdate = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            $"/api/v1/orders/{orderId}",
            intruder,
            new
            {
                discount = 0,
                tax = 0,
                items = new[] { new { productId, quantity = 1m } }
            });
        orderUpdate.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var orderDelete = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Delete, $"/api/v1/orders/{orderId}", intruder);
        orderDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var stillThere = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/orders/{orderId}", owner);
        stillThere.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SpoofedTenantHeader_IsRejected()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var other = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var customerId = await CreateCustomerAsync(owner);

        await AssertTenantMismatchAsync(owner, other.TenantId, $"/api/v1/customers/{customerId}");
        await AssertTenantMismatchAsync(owner, Guid.NewGuid(), "/api/v1/products?page=1&pageSize=10");
        await AssertTenantMismatchAsync(owner, other.TenantId, "/api/v1/orders?page=1&pageSize=10");
        await AssertTenantMismatchAsync(owner, other.TenantId, "/api/v1/finance/dashboard");
    }

    [Fact]
    public async Task AuthenticatedRequest_WithoutTenantHeader_UsesMembership()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products?page=1&pageSize=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);

        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task CreateUser_IgnoresClientSuppliedTenantId()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var other = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var email = $"staff_{Guid.NewGuid():N}@test.com";

        var created = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/users",
            owner,
            new
            {
                email,
                password = "Password1!",
                firstName = "Staff",
                lastName = "User",
                tenantId = other.TenantId
            });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("tenantId").GetGuid().Should().Be(owner.TenantId);

        var crossRead = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/v1/users/{body.GetProperty("id").GetString()}",
            other);
        crossRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivatedAccount_CannotReuseJwt()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var deactivate = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/users/{owner.UserId}/deactivate",
            owner);
        deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/products?page=1&pageSize=10",
            owner);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().Contain("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task RepeatedFailedLogins_LockTheAccount()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await Client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = owner.Email,
                password = "WrongPassword1!"
            });
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var locked = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = owner.Email,
            password = "Password1!"
        });

        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvalidJwt_IsRejected()
    {
        var response = await SendBearerAsync("this.is.not.a.jwt");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredJwt_IsRejected()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var expired = CreateToken(owner, DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(-5), IntegrationJwt.Key);

        var response = await SendBearerAsync(expired);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtSignedWithAnotherKey_IsRejected()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var forged = CreateToken(
            owner,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(30),
            "a-different-signing-key-that-is-long-enough");

        var response = await SendBearerAsync(forged);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnsignedJwt_IsRejected()
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode(
            $$"""{"sub":"user","TenantId":"{{Guid.NewGuid()}}","exp":9999999999}""");
        var token = $"{header}.{payload}.";

        var response = await SendBearerAsync(token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvalidProductPayload_IsRejected()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var categoryId = await CreateCategoryAsync(owner);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/products",
            owner,
            new
            {
                categoryId,
                name = "",
                sku = "BAD",
                costPrice = -5,
                salePrice = 0,
                reorderLevel = -1
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task AssertTenantMismatchAsync(AuthResponse owner, Guid spoofedTenantId, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-ID", spoofedTenantId.ToString());

        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("TENANT_MISMATCH");
    }

    private async Task<HttpResponseMessage> SendBearerAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products?page=1&pageSize=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("X-Tenant-ID", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }

    private static string CreateToken(AuthResponse owner, DateTime notBefore, DateTime expires, string key)
    {
        var token = new JwtSecurityToken(
            issuer: IntegrationJwt.Issuer,
            audience: IntegrationJwt.Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, owner.UserId),
                new Claim(ClaimTypes.Email, owner.Email),
                new Claim("TenantId", owner.TenantId.ToString()),
                new Claim(ClaimTypes.Role, RoleNames.Owner)
            ],
            notBefore: notBefore,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<Guid> CreateCustomerAsync(AuthResponse auth)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/customers",
            auth,
            new
            {
                firstName = "Ada",
                lastName = "Lovelace",
                email = $"ada_{Guid.NewGuid():N}@test.com",
                phoneNumber = "1234567890",
                address = "1 Analytical Engine",
                city = "London",
                country = "UK",
                postalCode = "SW1"
            });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateCategoryAsync(AuthResponse auth)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/categories",
            auth,
            new { name = $"Cat {Guid.NewGuid():N}" });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateOrderAsync(AuthResponse auth, Guid customerId, Guid productId)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/orders",
            auth,
            new
            {
                customerId,
                discount = 0,
                tax = 0,
                items = new[] { new { productId, quantity = 1m } }
            });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private Task<Guid> GetRoleIdAsync(string roleName)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        return Task.FromResult(context.RbacRoles.Single(x => x.Name == roleName).Id);
    }

    private async Task<AuthResponse> LoginAsync(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "Password1!"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Roles.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class CriticalWorkflowIntegrationTests : IntegrationTestBase
{
    public CriticalWorkflowIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData("/api/v1/expenses")]
    [InlineData("/api/v1/invoices")]
    [InlineData("/api/v1/payments")]
    [InlineData("/api/v1/suppliers")]
    [InlineData("/api/v1/quotations")]
    [InlineData("/api/v1/purchase-orders")]
    [InlineData("/api/v1/team")]
    [InlineData("/api/v1/reports/history")]
    public async Task ProtectedModules_RejectMissingTokens(string url)
    {
        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TamperedJwtPayload_IsRejected()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var parts = owner.Token.Split('.');
        var payload = parts[1].ToCharArray();
        payload[0] = payload[0] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}.{new string(payload)}.{parts[2]}";

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tampered);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MalformedJson_DoesNotLeakParserInternals()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-ID", owner.TenantId.ToString());
        request.Content = new StringContent("{not-json", Encoding.UTF8, "application/json");

        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotContain("StackTrace");
        body.Should().NotContain("at BusinessOS");
        body.Should().NotContain("Password");
    }

    [Fact]
    public async Task LoginFailure_DoesNotEchoThePassword()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        const string password = "WrongPassword1!";

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = owner.Email,
            password
        });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().NotContain(password);
    }

    [Fact]
    public async Task Order_RejectsAnotherTenantsProductAndCustomer()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var intruder = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var productId = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, owner, 5);
        var intruderCustomer = await CreateCustomerAsync(intruder);
        var intruderProduct = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, intruder, 5);

        var foreignProduct = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/orders",
            intruder,
            new
            {
                customerId = intruderCustomer,
                discount = 0,
                tax = 0,
                items = new[] { new { productId, quantity = 1m } }
            });
        foreignProduct.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var foreignCustomer = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/orders",
            intruder,
            new
            {
                customerId = await CreateCustomerAsync(owner),
                discount = 0,
                tax = 0,
                items = new[] { new { productId = intruderProduct, quantity = 1m } }
            });
        foreignCustomer.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var stock = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/inventory/{productId}", owner);
        stock.EnsureSuccessStatusCode();
        var inventory = await stock.Content.ReadFromJsonAsync<JsonElement>();
        inventory.GetProperty("currentStock").GetDecimal().Should().Be(5);
    }

    [Fact]
    public async Task DuplicateOrderPosts_AreNotIdempotent()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var customerId = await CreateCustomerAsync(owner);
        var productId = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, owner, 10);
        var body = new
        {
            customerId,
            discount = 0,
            tax = 0,
            items = new[] { new { productId, quantity = 1m } }
        };

        var first = await IntegrationHttp.SendAuthorizedAsync(Client, HttpMethod.Post, "/api/v1/orders", owner, body);
        var second = await IntegrationHttp.SendAuthorizedAsync(Client, HttpMethod.Post, "/api/v1/orders", owner, body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        secondId.Should().NotBe(firstId);

        var stock = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/inventory/{productId}", owner);
        var inventory = await stock.Content.ReadFromJsonAsync<JsonElement>();
        inventory.GetProperty("currentStock").GetDecimal().Should().Be(8);
    }

    [Fact]
    public async Task NegativeStockIncrease_IsRejected()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var productId = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, owner, 4);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/inventory/increase",
            owner,
            new { productId, quantity = -3, notes = "tamper" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var stock = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/inventory/{productId}", owner);
        var inventory = await stock.Content.ReadFromJsonAsync<JsonElement>();
        inventory.GetProperty("currentStock").GetDecimal().Should().Be(4);
    }

    [Fact]
    public async Task Expense_IsRounded_Isolated_AndRejectsZero()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var other = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var category = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/expense-categories",
            owner,
            new { name = $"Rent {Guid.NewGuid():N}", description = "Office" });
        category.EnsureSuccessStatusCode();
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var invalid = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/expenses",
            owner,
            new
            {
                title = "Zero",
                amount = 0,
                expenseDate = DateTime.UtcNow,
                expenseCategoryId = categoryId,
                paymentMethod = "Cash",
                status = "Pending",
                isRecurring = false
            });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/expenses",
            owner,
            new
            {
                title = "  Office  ",
                amount = 10.126m,
                expenseDate = DateTime.UtcNow,
                expenseCategoryId = categoryId,
                paymentMethod = "Cash",
                status = "Pending",
                isRecurring = false
            });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var expenseId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var hidden = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/expenses/{expenseId}", other);
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var visible = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/expenses/{expenseId}", owner);
        visible.EnsureSuccessStatusCode();
        (await visible.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("amount").GetDecimal().Should().Be(10.13m);
    }

    [Fact]
    public async Task Finance_RejectsInvalidPeriod_AndEmployeeAccess()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/finance/profit-loss?groupBy=week",
            owner);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var employee = await AsEmployeeAsync(owner);
        var denied = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/expenses", employee);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HostileCustomerName_IsStoredAsData()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var payload = "'; DROP TABLE Customers;-- <script>alert(1)</script>";
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/customers",
            owner,
            new
            {
                firstName = payload,
                lastName = "Safe",
                email = $"safe_{Guid.NewGuid():N}@test.com",
                phoneNumber = "1234567890",
                address = "1 Road",
                city = "Lahore",
                country = "Pakistan",
                postalCode = "54000"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var read = await IntegrationHttp.SendAuthorizedAsync(Client, HttpMethod.Get, $"/api/v1/customers/{id}", owner);
        read.EnsureSuccessStatusCode();
        var customer = await read.Content.ReadFromJsonAsync<JsonElement>();
        customer.GetProperty("firstName").GetString().Should().Be(payload);
        read.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public void BusinessEndpoints_RequireAuthorizationUnlessExplicitlyPublic()
    {
        var endpoints = Factory.Services.GetRequiredService<EndpointDataSource>();
        var missing = new List<string>();

        foreach (var endpoint in endpoints.Endpoints.OfType<RouteEndpoint>())
        {
            var route = "/" + endpoint.RoutePattern.RawText?.TrimStart('/');
            if (IsPublic(route))
                continue;

            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var authorized = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any();
            if (anonymous || !authorized)
                missing.Add(route);
        }

        missing.Should().BeEmpty();
    }

    private static bool IsPublic(string route)
    {
        var path = route.ToLowerInvariant();
        return path.Contains("/auth")
            || path.Contains("register-business")
            || path.Contains("webhook")
            || path.Contains("/health")
            || path.Contains("openapi")
            || path.Contains("scalar")
            || path.Contains("swagger")
            || path.Contains("/billing/plans");
    }

    private async Task<AuthResponse> AsEmployeeAsync(AuthResponse owner)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.BusinessOSDbContext>();
        var ownerRoleId = context.RbacRoles.Single(x => x.Name == RoleNames.Owner).Id;
        var employeeRoleId = context.RbacRoles.Single(x => x.Name == RoleNames.Employee).Id;

        var assign = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/users/{owner.UserId}/roles",
            owner,
            new AssignUserRoleRequest(employeeRoleId));
        assign.EnsureSuccessStatusCode();

        var interim = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = owner.Email,
            password = "Password1!"
        });
        interim.EnsureSuccessStatusCode();
        var refreshedOwner = (await interim.Content.ReadFromJsonAsync<AuthResponse>())!;

        var remove = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Delete, $"/api/v1/users/{owner.UserId}/roles/{ownerRoleId}", refreshedOwner);
        remove.EnsureSuccessStatusCode();

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = owner.Email,
            password = "Password1!"
        });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
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
                address = "1 Road",
                city = "London",
                country = "UK",
                postalCode = "SW1"
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}

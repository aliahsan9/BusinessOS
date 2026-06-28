using System.Net;
using System.Net.Http.Json;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Dashboard.DTOs;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Roles.DTOs;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
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
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/products");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.value");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ViewerUser_CannotCreateCategory_ReturnsForbidden()
    {
        var adminAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await SwitchToViewerRoleAsync(adminAuth);

        var viewerAuth = await LoginAsync(adminAuth.Email);
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            viewerAuth,
            new { name = "Blocked Category" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ViewerUser_CannotDeleteOrder_ReturnsForbidden()
    {
        var adminAuth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await SwitchToViewerRoleAsync(adminAuth);
        var viewerAuth = await LoginAsync(adminAuth.Email);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/orders/{Guid.NewGuid()}",
            viewerAuth);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task SwitchToViewerRoleAsync(AuthResponse adminAuth)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BusinessOS.Infrastructure.Data.BusinessOSDbContext>();
        var adminRoleId = context.RbacRoles.Single(x => x.Name == BusinessOS.Application.Common.Authorization.RoleNames.Admin).Id;
        var viewerRoleId = context.RbacRoles.Single(x => x.Name == BusinessOS.Application.Common.Authorization.RoleNames.Viewer).Id;

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

[Collection("IntegrationTests")]
public class ExtendedFlowIntegrationTests : IntegrationTestBase
{
    public ExtendedFlowIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var email = $"dup_{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email);

        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            firstName = "Test",
            lastName = "User",
            businessName = "Test Business"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AdminUser_CanCreateRole()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var roleName = $"Auditor_{Guid.NewGuid():N}";

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/roles",
            auth,
            new CreateRoleRequest(roleName, "Audit-only role"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var role = await response.Content.ReadFromJsonAsync<RoleDto>();
        role!.Name.Should().Be(roleName);
    }

    [Fact]
    public async Task GetOutOfStockProducts_ReturnsZeroStockItems()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, auth, 0);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/inventory/out-of-stock",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await response.Content.ReadFromJsonAsync<List<InventorySummaryResponse>>();
        items.Should().NotBeNull();
        items!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetReorderProducts_ReturnsLowStockItems()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var productId = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, auth, 2);

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            $"/api/inventory/{productId}",
            auth,
            new { minimumStockLevel = 0, maximumStockLevel = 100, reorderLevel = 10 });

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/inventory/reorder-products",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = await response.Content.ReadFromJsonAsync<List<InventorySummaryResponse>>();
        items!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AdjustStock_WithValidType_UpdatesInventory()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var productId = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, auth, 10);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/inventory/adjust",
            auth,
            new
            {
                productId,
                quantity = 2,
                transactionType = "Adjustment",
                notes = "Cycle count correction"
            });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetStockTransactions_ReturnsPagedHistory()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var productId = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, auth, 15);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/inventory/transactions?productId={productId}&page=1&pageSize=10",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOrderAnalyticsDashboard_ReturnsMetrics()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/orders?period=all",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var analytics = await response.Content.ReadFromJsonAsync<OrderAnalyticsResponse>();
        analytics.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCustomerAnalyticsDashboard_ReturnsMetrics()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/customers?period=all",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var analytics = await response.Content.ReadFromJsonAsync<CustomerAnalyticsDashboardResponse>();
        analytics.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrdersAndCustomersCharts_ReturnChartPayload()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var ordersChart = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/charts/orders?period=all",
            auth);

        ordersChart.StatusCode.Should().Be(HttpStatusCode.OK);

        var customersChart = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/charts/customers?period=all",
            auth);

        customersChart.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task RegisterAsync(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            firstName = "Test",
            lastName = "User",
            businessName = "Test Business"
        });

        response.EnsureSuccessStatusCode();
    }
}

[Collection("IntegrationTests")]
public class DatabaseConstraintTests : IntegrationTestBase
{
    public DatabaseConstraintTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_ReturnsConflict()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        const string name = "Unique Category Name";

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name });

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteCustomer_ReturnsNoContentEvenWhenOrdersExist()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var customerId = await CreateCustomerWithOrderAsync(auth);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/customers/{customerId}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<Guid> CreateCustomerWithOrderAsync(AuthResponse auth)
    {
        var customerResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/customers",
            auth,
            new
            {
                firstName = "Ali",
                lastName = "Ahsan",
                email = $"fk_{Guid.NewGuid():N}@test.com",
                phoneNumber = "1234567890",
                address = "123 Main St",
                city = "Lahore",
                country = "Pakistan",
                postalCode = "54000"
            });

        customerResponse.EnsureSuccessStatusCode();
        var customer = await customerResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var customerId = customer.GetProperty("id").GetGuid();
        var productId = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, auth, 10);

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/orders",
            auth,
            new
            {
                customerId,
                discount = 0,
                tax = 0,
                items = new[] { new { productId, quantity = 1m } }
            });

        return customerId;
    }
}

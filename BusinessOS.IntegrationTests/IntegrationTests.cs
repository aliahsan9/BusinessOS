using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Categories.Queries;
using BusinessOS.Application.Features.Orders.Queries;
using BusinessOS.Application.Features.Products.Queries;
using FluentAssertions;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class AuthIntegrationTests : IntegrationTestBase
{
    public AuthIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Register_ReturnsCreatedWithToken()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";

        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            firstName = "Test",
            lastName = "User",
            businessName = "Test Business"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Content.Headers.ContentType?.MediaType.Should().Contain("json");

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.Token.Should().NotBeNullOrWhiteSpace();
        auth.TenantId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        var email = $"login_{Guid.NewGuid():N}@test.com";
        await RegisterUserAsync(email);

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorizedProblemDetails()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "missing@test.com",
            password = "WrongPass1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsValidationProblemDetails()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email",
            password = "Password1!",
            firstName = "Test",
            lastName = "User",
            businessName = "Test Business"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/categories");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Register_TokenCanAccessProtectedCategories()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name = "Authorized Category" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<AuthResponse> RegisterUserAsync(string email)
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
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}

[Collection("IntegrationTests")]
public class CategoryIntegrationTests : IntegrationTestBase
{
    public CategoryIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateCategory_ReturnsCreated()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name = "Electronics", description = "Electronic items" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetCategories_ReturnsPagedResult()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await CreateCategoryAsync(auth, "Books");
        await CreateCategoryAsync(auth, "Games");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/categories?page=1&pageSize=1&sortBy=name",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<CategoryDto>>();
        page.Should().NotBeNull();
        page!.Items.Should().HaveCount(1);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task GetCategory_ReturnsCategory()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var createResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name = "Books", description = "Reading materials" });

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/categories/{id}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category!.Name.Should().Be("Books");
    }

    [Fact]
    public async Task GetCategory_WithUnknownId_ReturnsNotFoundProblemDetails()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/categories/{Guid.NewGuid()}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task UpdateCategory_ReturnsNoContent()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var id = await CreateCategoryAsync(auth, "Old Name");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            $"/api/categories/{id}",
            auth,
            new { name = "Updated Name", description = "Updated" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteCategory_ReturnsNoContent()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var id = await CreateCategoryAsync(auth, "Temporary");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/categories/{id}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateCategory_WithEmptyName_ReturnsBadRequest()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name = "", description = "Invalid" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteCategory_WithProducts_ReturnsConflict()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var categoryId = await CreateCategoryAsync(auth, "With Products");

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/products",
            auth,
            new
            {
                categoryId,
                name = "Item",
                sku = "SKU-DEL",
                costPrice = 1,
                salePrice = 2,
                reorderLevel = 1
            });

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/categories/{categoryId}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<Guid> CreateCategoryAsync(AuthResponse auth, string name)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }
}

[Collection("IntegrationTests")]
public class ProductIntegrationTests : IntegrationTestBase
{
    public ProductIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreated()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var categoryId = await CreateCategoryAsync(auth, "Product Category");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/products",
            auth,
            new
            {
                categoryId,
                name = "Laptop",
                sku = "SKU-001",
                description = "A laptop",
                costPrice = 500,
                salePrice = 799,
                reorderLevel = 5
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetProducts_ReturnsPagedFilteredResults()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var categoryId = await CreateCategoryAsync(auth, "Paged Category");
        await CreateProductAsync(auth, categoryId, "Alpha", "SKU-A");
        await CreateProductAsync(auth, categoryId, "Beta", "SKU-B");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/products?categoryId={categoryId}&search=Alpha&page=1&pageSize=10&sortBy=name",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        page!.Items.Should().ContainSingle(x => x.Name == "Alpha");
        page.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetProduct_ReturnsProduct()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var categoryId = await CreateCategoryAsync(auth, "Get Product Category");
        var productId = await CreateProductAsync(auth, categoryId, "Phone", "SKU-002");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/products/{productId}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProduct_WithUnknownId_ReturnsNotFound()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/products/{Guid.NewGuid()}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductsByCategory_ReturnsPagedResult()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var categoryId = await CreateCategoryAsync(auth, "By Category");
        await CreateProductAsync(auth, categoryId, "Item 1", "SKU-C1");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/products/by-category/{categoryId}?page=1&pageSize=10",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        page!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UpdateProduct_ReturnsNoContent()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var categoryId = await CreateCategoryAsync(auth, "Update Category");
        var productId = await CreateProductAsync(auth, categoryId, "Mouse", "SKU-003");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            $"/api/products/{productId}",
            auth,
            new
            {
                categoryId,
                name = "Wireless Mouse",
                sku = "SKU-003",
                description = "Updated",
                costPrice = 10,
                salePrice = 20,
                reorderLevel = 2,
                isActive = true
            });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsNoContent()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var categoryId = await CreateCategoryAsync(auth, "Delete Category");
        var productId = await CreateProductAsync(auth, categoryId, "Keyboard", "SKU-004");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/products/{productId}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateProduct_WithInvalidCategory_ReturnsBadRequest()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/products",
            auth,
            new
            {
                categoryId = Guid.NewGuid(),
                name = "Invalid",
                sku = "SKU-X",
                costPrice = 1,
                salePrice = 2,
                reorderLevel = 1
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_WithInvalidPrice_ReturnsBadRequest()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var categoryId = await CreateCategoryAsync(auth, "Validation Category");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/products",
            auth,
            new
            {
                categoryId,
                name = "Bad Price",
                sku = "SKU-BAD",
                costPrice = 0,
                salePrice = -1,
                reorderLevel = 1
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateCategoryAsync(AuthResponse auth, string name)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateProductAsync(
        AuthResponse auth,
        Guid categoryId,
        string name,
        string sku)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/products",
            auth,
            new
            {
                categoryId,
                name,
                sku,
                costPrice = 10,
                salePrice = 20,
                reorderLevel = 1
            });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }
}

[Collection("IntegrationTests")]
public class OrderIntegrationTests : IntegrationTestBase
{
    public OrderIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateOrder_ReturnsCreated()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var productId = await CreateProductAsync(auth);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/orders",
            auth,
            new
            {
                customerName = "Ali Ahsan",
                customerEmail = "ali@test.com",
                customerPhone = "1234567890",
                customerAddress = "123 Main St",
                discount = 0,
                tax = 0,
                items = new[] { new { productId, quantity = 2m } }
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetOrders_ReturnsPagedResult()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await CreateOrderAsync(auth);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/orders?page=1&pageSize=10&sortBy=createdAt&sortOrder=desc",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<OrderSummaryDto>>();
        page.Should().NotBeNull();
        page!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetOrder_ReturnsOrderDetails()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var orderId = await CreateOrderAsync(auth);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/orders/{orderId}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order!.Items.Should().NotBeEmpty();
        order.CustomerName.Should().Be("Ali Ahsan");
    }

    [Fact]
    public async Task GetOrder_WithUnknownId_ReturnsNotFound()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/orders/{Guid.NewGuid()}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOrder_ReturnsNoContent()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var orderId = await CreateOrderAsync(auth);

        var getResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/orders/{orderId}",
            auth);

        var order = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
        var productId = order!.Items.First().ProductId;

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            $"/api/orders/{orderId}",
            auth,
            new
            {
                customerName = "Ali Updated",
                customerEmail = order.CustomerEmail,
                customerPhone = order.CustomerPhone,
                customerAddress = order.CustomerAddress,
                discount = 1,
                tax = 1,
                items = new[] { new { productId, quantity = 3m } }
            });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteOrder_ReturnsNoContent()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var orderId = await CreateOrderAsync(auth);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Delete,
            $"/api/orders/{orderId}",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateOrderStatus_ReturnsNoContent()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var orderId = await CreateOrderAsync(auth);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Patch,
            $"/api/orders/{orderId}/status",
            auth,
            new { status = "Confirmed" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithInvalidTransition_ReturnsBadRequest()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var orderId = await CreateOrderAsync(auth);

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Patch,
            $"/api/orders/{orderId}/status",
            auth,
            new { status = "Confirmed" });

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Patch,
            $"/api/orders/{orderId}/status",
            auth,
            new { status = "Processing" });

        await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Patch,
            $"/api/orders/{orderId}/status",
            auth,
            new { status = "Completed" });

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Patch,
            $"/api/orders/{orderId}/status",
            auth,
            new { status = "Pending" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidProduct_ReturnsBadRequest()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/orders",
            auth,
            new
            {
                customerName = "Ali",
                customerEmail = "ali@test.com",
                customerPhone = "",
                customerAddress = "",
                discount = 0,
                tax = 0,
                items = new[] { new { productId = Guid.NewGuid(), quantity = 1m } }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidQuantity_ReturnsBadRequest()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var productId = await CreateProductAsync(auth);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/orders",
            auth,
            new
            {
                customerName = "Ali",
                customerEmail = "ali@test.com",
                customerPhone = "",
                customerAddress = "",
                discount = 0,
                tax = 0,
                items = new[] { new { productId, quantity = 0m } }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProtectedOrdersEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/orders");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateCategoryAsync(AuthResponse auth, string name)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateProductAsync(AuthResponse auth)
    {
        var categoryId = await CreateCategoryAsync(auth, $"Order Cat {Guid.NewGuid():N}");

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/products",
            auth,
            new
            {
                categoryId,
                name = "Order Product",
                sku = $"SKU-{Guid.NewGuid():N}",
                costPrice = 10,
                salePrice = 25,
                reorderLevel = 1
            });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateOrderAsync(AuthResponse auth)
    {
        var productId = await CreateProductAsync(auth);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/orders",
            auth,
            new
            {
                customerName = "Ali Ahsan",
                customerEmail = $"ali_{Guid.NewGuid():N}@test.com",
                customerPhone = "123",
                customerAddress = "Address",
                discount = 0,
                tax = 0,
                items = new[] { new { productId, quantity = 1m } }
            });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Categories.Queries;
using FluentAssertions;

namespace BusinessOS.IntegrationTests;

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
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "missing@test.com",
            password = "WrongPass1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

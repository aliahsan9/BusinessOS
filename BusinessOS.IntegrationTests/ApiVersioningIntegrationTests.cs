using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessOS.API.Versioning;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class ApiVersioningIntegrationTests : IntegrationTestBase
{
    private static readonly string[] VersionedResources =
    [
        "/auth",
        "/account",
        "/categories",
        "/products",
        "/customers",
        "/orders",
        "/suppliers",
        "/purchase-orders",
        "/payments",
        "/invoices",
        "/quotations",
        "/inventory",
        "/dashboard",
        "/analytics",
        "/reports",
        "/roles",
        "/permissions",
        "/expenses",
        "/expense-categories",
        "/finance",
        "/users",
        "/audit-logs",
        "/notifications",
        "/activity",
        "/settings",
        "/onboarding",
        "/ai",
        "/agents",
        "/help",
        "/team",
        "/organization",
        "/tenant",
        "/billing",
        "/register-business",
        "/system"
    ];

    public ApiVersioningIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Version1Request_SucceedsAndReportsSupportedVersion()
    {
        var response = await Client.GetAsync("/api/v1/billing/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(ApiVersioningPaths.SupportedVersionsHeader)
            .Should().Contain(value => value.Contains("1.0", StringComparison.Ordinal));
        response.Headers.Contains(ApiVersioningPaths.DeprecatedVersionsHeader).Should().BeFalse();
    }

    [Fact]
    public async Task Version1DotZeroUrl_Succeeds()
    {
        var response = await Client.GetAsync("/api/v1.0/billing/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void BusinessEndpoints_AreRegisteredOnlyUnderVersion1()
    {
        var patterns = Factory.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToList();

        foreach (var resource in VersionedResources)
        {
            patterns.Should().Contain(pattern =>
                pattern.Contains("/api/v{version:apiVersion}" + resource, StringComparison.OrdinalIgnoreCase) ||
                pattern.Contains("api/v{version:apiVersion}" + resource, StringComparison.OrdinalIgnoreCase));
        }

        var unexpected = patterns
            .Where(pattern => pattern.Contains("/api/", StringComparison.OrdinalIgnoreCase))
            .Where(pattern => !pattern.Contains("version:apiVersion", StringComparison.OrdinalIgnoreCase))
            .Where(pattern => !pattern.Contains("/api/health", StringComparison.OrdinalIgnoreCase))
            .Where(pattern => !pattern.Contains("/api/billing/webhook/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        unexpected.Should().BeEmpty();
        patterns.Should().Contain(pattern => pattern.Contains("/api/health", StringComparison.OrdinalIgnoreCase));
        patterns.Should().Contain("/api/billing/webhook/stripe");
        patterns.Should().NotContain(pattern => pattern.Contains("/api/v{version:apiVersion}/health", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnknownApiVersion_IsRejectedAndReportsSupportedVersions()
    {
        var response = await Client.GetAsync("/api/v2/customers");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain(ApiVersioningPaths.VersionUnsupportedCode);
        body.Should().NotContain(ApiVersioningPaths.VersionRequiredCode);
        response.Headers.GetValues(ApiVersioningPaths.SupportedVersionsHeader)
            .Should().Contain(value => value.Contains("1.0", StringComparison.Ordinal));
        var supported = string.Join(',', response.Headers.GetValues(ApiVersioningPaths.SupportedVersionsHeader));
        supported.Should().NotContain("2.0");
    }

    [Fact]
    public async Task MissingApiVersion_ReturnsVersionRequiredProblem()
    {
        var response = await Client.GetAsync("/api/customers");
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        document.GetProperty("code").GetString().Should().Be(ApiVersioningPaths.VersionRequiredCode);
        document.GetProperty("status").GetInt32().Should().Be(400);
        document.GetProperty("detail").GetString().Should().Contain("/api/v1/customers");
        response.Headers.GetValues(ApiVersioningPaths.SupportedVersionsHeader)
            .Should().Contain(ApiVersioningPaths.CurrentVersion);
    }

    [Fact]
    public async Task VersionedEndpoint_StillRequiresAuthentication()
    {
        var response = await Client.GetAsync("/api/v1/customers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VersionedEndpoint_StillRequiresTenant()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var otherTenant = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        owner.TenantId.Should().NotBe(Guid.Empty);
        otherTenant.TenantId.Should().NotBe(owner.TenantId);

        var email = $"ada_{Guid.NewGuid():N}@test.com";
        var created = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/customers",
            owner,
            new
            {
                firstName = "Ada",
                lastName = "Lovelace",
                email,
                phoneNumber = "1234567890",
                address = "1 Analytical Engine",
                city = "London",
                country = "UK",
                postalCode = "SW1"
            });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        using var mismatched = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers?page=1&pageSize=10");
        mismatched.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        mismatched.Headers.TryAddWithoutValidation("X-Tenant-ID", otherTenant.TenantId.ToString());

        var response = await Client.SendAsync(mismatched);
        var body = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            body.Should().Contain("TENANT_MISMATCH");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain(email);
    }

    [Fact]
    public async Task HealthEndpoint_StaysUnversioned()
    {
        var response = await Client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LegacyBillingWebhook_RemainsReachable()
    {
        var legacy = await Client.PostAsJsonAsync("/api/billing/webhook/stripe", new { });
        var versioned = await Client.PostAsJsonAsync("/api/v1/billing/webhook/stripe", new { });

        legacy.StatusCode.Should().Be(HttpStatusCode.OK);
        versioned.StatusCode.Should().Be(HttpStatusCode.OK);

        var legacyBody = await legacy.Content.ReadAsStringAsync();
        legacyBody.Should().NotContain(ApiVersioningPaths.VersionRequiredCode);
    }

    [Fact]
    public async Task OpenApiDocument_ContainsVersion1RoutesWithoutDuplicates()
    {
        var response = await Client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var operationIds = new List<string>();
        var pathKeys = new List<string>();

        foreach (var path in paths.EnumerateObject())
        {
            pathKeys.Add(path.Name);
            path.Name.Should().NotContain("{version}");

            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!IsHttpMethod(operation.Name))
                {
                    continue;
                }

                if (operation.Value.TryGetProperty("operationId", out var operationId))
                {
                    operationIds.Add(operationId.GetString()!);
                }
            }
        }

        pathKeys.Should().Contain(path => path.Contains("/api/v1/customers", StringComparison.OrdinalIgnoreCase));
        pathKeys.Should().Contain(path => path.Contains("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase));
        pathKeys.Should().NotContain(path => path.Contains("/api/v2/", StringComparison.OrdinalIgnoreCase));
        pathKeys.Should().NotContain("/api/customers");
        pathKeys.Should().NotContain(path => path.Contains("/api/billing/webhook/stripe", StringComparison.OrdinalIgnoreCase)
            && !path.Contains("/api/v1/", StringComparison.OrdinalIgnoreCase));
        operationIds.Should().OnlyHaveUniqueItems();

        var missingDocument = await Client.GetAsync("/openapi/v2.json");
        missingDocument.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScalarReference_IsAvailable()
    {
        var response = await Client.GetAsync("/scalar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Contain("text/html");
    }

    private static bool IsHttpMethod(string name) =>
        name.Equals("get", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("post", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("put", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("patch", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("delete", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("head", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("options", StringComparison.OrdinalIgnoreCase);
}

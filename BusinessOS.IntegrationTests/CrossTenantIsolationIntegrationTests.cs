using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Billing.DTOs;
using FluentAssertions;

namespace BusinessOS.IntegrationTests;

/// <summary>
/// Automated regression suite for Phase 4 tenant isolation.
/// Membership tenant is authoritative; client-supplied tenant IDs must never grant cross-tenant access.
/// </summary>
[Collection("IntegrationTests")]
public class CrossTenantIsolationIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CrossTenantIsolationIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task AiConversations_AreIsolatedPerTenant()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var intruder = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var create = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Post, "/api/v1/ai/conversations", owner);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = await create.Content.ReadFromJsonAsync<AiConversationSessionDto>(JsonOptions);
        session.Should().NotBeNull();

        var foreignGet = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/ai/conversations/{session!.Id}", intruder);
        foreignGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var foreignDelete = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Delete, $"/api/v1/ai/conversations/{session.Id}", intruder);
        foreignDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var ownerList = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/ai/conversations", owner);
        ownerList.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerSessions = await ownerList.Content.ReadFromJsonAsync<List<AiConversationSessionDto>>(JsonOptions);
        ownerSessions.Should().Contain(x => x.Id == session.Id);

        var intruderList = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/ai/conversations", intruder);
        intruderList.StatusCode.Should().Be(HttpStatusCode.OK);
        var intruderSessions = await intruderList.Content.ReadFromJsonAsync<List<AiConversationSessionDto>>(JsonOptions);
        intruderSessions.Should().NotContain(x => x.Id == session.Id);
    }

    [Fact]
    public async Task BillingUsage_DoesNotLeakAcrossTenants()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var other = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        // Seed a product so usage counters differ between tenants.
        await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, owner, 3);

        var ownerUsage = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/billing/usage", owner);
        ownerUsage.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerDto = await ownerUsage.Content.ReadFromJsonAsync<BillingUsageDto>(JsonOptions);

        var otherUsage = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/billing/usage", other);
        otherUsage.StatusCode.Should().Be(HttpStatusCode.OK);
        var otherDto = await otherUsage.Content.ReadFromJsonAsync<BillingUsageDto>(JsonOptions);

        ownerDto.Should().NotBeNull();
        otherDto.Should().NotBeNull();
        ownerDto!.ProductCount.Should().BeGreaterThan(0);
        otherDto!.ProductCount.Should().Be(0);
    }

    [Fact]
    public async Task TenantFiles_AreIsolatedPerTenant()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var intruder = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        using var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent("tenant-a-secret"u8.ToArray());
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(bytes, "file", "secret.txt");

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenant/files")
        {
            Content = content
        };
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);
        uploadRequest.Headers.TryAddWithoutValidation("X-Tenant-ID", owner.TenantId.ToString());

        var upload = await Client.SendAsync(uploadRequest);
        upload.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var uploaded = await upload.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var fileId = uploaded.TryGetProperty("id", out var idProp)
            ? idProp.GetGuid()
            : Guid.Empty;
        fileId.Should().NotBe(Guid.Empty);

        var foreignGet = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/tenant/files/{fileId}", intruder);
        foreignGet.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);

        var foreignDelete = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Delete, $"/api/v1/tenant/files/{fileId}", intruder);
        foreignDelete.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);

        var ownerGet = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/tenant/files/{fileId}", owner);
        ownerGet.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SpoofedTenantHeader_IsRejected_ForAiAndBilling()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var other = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        await AssertTenantMismatchAsync(owner, other.TenantId, "/api/v1/ai/conversations");
        await AssertTenantMismatchAsync(owner, other.TenantId, "/api/v1/billing/usage");
        await AssertTenantMismatchAsync(owner, other.TenantId, "/api/v1/billing/subscription");
        await AssertTenantMismatchAsync(owner, Guid.NewGuid(), "/api/v1/tenant/files");
    }

    [Fact]
    public async Task Settings_AreIsolatedPerTenant()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var other = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var ownerUpdate = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/settings",
            owner,
            new
            {
                currency = "USD",
                language = "en",
                taxRate = 7.5m,
                invoicePrefix = "OWN",
                emailFromAddress = owner.Email,
                theme = "light",
                logoUrl = (string?)null,
                timezone = "UTC",
                aiAssistantEnabled = true,
                aiShowSuggestions = true,
                emailNotificationsEnabled = true,
                systemNotificationsEnabled = true,
                orderNotificationsEnabled = true,
                inventoryAlertsEnabled = true,
                paymentAlertsEnabled = true,
                taskNotificationsEnabled = true,
                invoiceNotificationsEnabled = true,
                customerNotificationsEnabled = true,
                projectNotificationsEnabled = true
            });
        ownerUpdate.EnsureSuccessStatusCode();

        var otherUpdate = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/settings",
            other,
            new
            {
                currency = "PKR",
                language = "en",
                taxRate = 0m,
                invoicePrefix = "OTH",
                emailFromAddress = other.Email,
                theme = "dark",
                logoUrl = (string?)null,
                timezone = "Asia/Karachi",
                aiAssistantEnabled = true,
                aiShowSuggestions = false,
                emailNotificationsEnabled = true,
                systemNotificationsEnabled = true,
                orderNotificationsEnabled = true,
                inventoryAlertsEnabled = true,
                paymentAlertsEnabled = true,
                taskNotificationsEnabled = true,
                invoiceNotificationsEnabled = true,
                customerNotificationsEnabled = true,
                projectNotificationsEnabled = true
            });
        otherUpdate.EnsureSuccessStatusCode();

        var ownerSettings = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/settings", owner);
        var otherSettings = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/settings", other);

        ownerSettings.StatusCode.Should().Be(HttpStatusCode.OK);
        otherSettings.StatusCode.Should().Be(HttpStatusCode.OK);

        var ownerBody = await ownerSettings.Content.ReadAsStringAsync();
        var otherBody = await otherSettings.Content.ReadAsStringAsync();
        ownerBody.Should().Contain("OWN");
        ownerBody.Should().Contain("7.5");
        otherBody.Should().Contain("OTH");
        otherBody.Should().NotContain("OWN");
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

}

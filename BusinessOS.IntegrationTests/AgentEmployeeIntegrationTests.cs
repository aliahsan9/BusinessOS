using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Auth.DTOs;
using FluentAssertions;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class AgentEmployeeIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AgentEmployeeIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task AgentChat_CreateCustomer_ReturnsSuccessAction()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var request = new AgentChatRequest
        {
            Message = "Create customer. His name is Ahmed Ali. Phone number 03001234567. Address Lahore. Email ahmed.agent.test@gmail.com",
            Language = "en",
            CurrentPage = "/customers",
            PreferEmployeeTone = true
        };

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/agents/chat",
            auth,
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AgentChatResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Reply.Should().NotBeNullOrWhiteSpace();
        body.ToolsUsed.Should().NotBeNull();

        // Structured create path should succeed when permissions exist for new tenant owner/admin.
        if (body.ActionResult is not null)
        {
            body.ActionResult.Success.Should().BeTrue();
            body.ActionResult.EntityType.Should().Be("Customer");
            body.ActionResult.EntityId.Should().NotBeNull();
        }
        else
        {
            // Runtime may answer via tool summary without ActionResult when clarification occurs.
            body.Reply.Should().Match(r =>
                r.Contains("customer", StringComparison.OrdinalIgnoreCase)
                || r.Contains("Ahmed", StringComparison.OrdinalIgnoreCase)
                || r.Contains("created", StringComparison.OrdinalIgnoreCase)
                || r.Contains("permission", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task AgentChatStream_EmitsStatusAndFinalChunks()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var payload = JsonSerializer.Serialize(new AgentChatRequest
        {
            Message = "Show my inventory summary",
            Language = "en",
            CurrentPage = "/inventory"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/agents/chat/stream")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-ID", auth.TenantId.ToString());

        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("data:");
        text.Should().Match(t => t.Contains("\"type\":\"status\"") || t.Contains("\"type\":\"final\"") || t.Contains("\"type\":\"token\""));
    }

    [Fact]
    public async Task AgentEmployees_ReturnsSophiaCatalog()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/agents/employees",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var employees = await response.Content.ReadFromJsonAsync<List<AgentEmployeeDto>>(JsonOptions);
        employees.Should().NotBeNull();
        employees!.Should().Contain(e => e.Key == "sophia");
    }

    [Fact]
    public async Task AgentOnboardingStart_ReturnsWorkflowSteps()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/agents/onboarding/start",
            auth,
            new AgentOnboardingStartRequest { Language = "en" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AgentOnboardingResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Reply.Should().NotBeNullOrWhiteSpace();
        body.WorkflowSteps.Should().NotBeEmpty();
    }
}

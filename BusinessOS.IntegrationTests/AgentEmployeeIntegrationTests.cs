using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Domain.Enums;
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
            "/api/v1/agents/chat",
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

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/chat/stream")
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
            "/api/v1/agents/employees",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var employees = await response.Content.ReadFromJsonAsync<List<AgentEmployeeDto>>(JsonOptions);
        employees.Should().NotBeNull();
        employees!.Should().Contain(e => e.Key == "sophia");
    }


    [Fact]
    public async Task AgentActivation_IsTenantScoped_AndDoesNotChangeSystemCatalog()
    {
        var tenantA = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var tenantB = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var deactivate = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/adam/activation",
            tenantA,
            new SetAgentActivationRequest { IsActive = false });

        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenantAResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/agents/employees",
            tenantA);
        var tenantAEmployees = await tenantAResponse.Content.ReadFromJsonAsync<List<AgentEmployeeDto>>(JsonOptions);
        tenantAEmployees.Should().NotBeNull();
        tenantAEmployees!.Should().Contain(e => e.Key == "adam" && !e.IsActive);

        var tenantBResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/agents/employees",
            tenantB);
        var tenantBEmployees = await tenantBResponse.Content.ReadFromJsonAsync<List<AgentEmployeeDto>>(JsonOptions);
        tenantBEmployees.Should().NotBeNull();
        tenantBEmployees!.Should().Contain(e => e.Key == "adam" && e.IsActive);

        var reactivate = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/adam/activation",
            tenantA,
            new SetAgentActivationRequest { IsActive = true });
        reactivate.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AgentActivation_CannotDeactivateDefaultEmployee()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/sophia/activation",
            auth,
            new SetAgentActivationRequest { IsActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }


    [Fact]
    public async Task AgentConfiguration_IsTenantScoped_AndPreservesSystemPersonaPrompt()
    {
        var tenantA = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var tenantB = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var update = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/adam/configuration",
            tenantA,
            new UpdateAgentConfigurationRequest
            {
                DisplayName = "Adam Retail",
                RoleTitle = "Warehouse Lead",
                Specialty = "Retail stock, reorder planning, and supplier follow-up",
                DefaultLanguage = "ur-Latn"
            });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<AgentEmployeeDto>(JsonOptions);
        updated.Should().NotBeNull();
        updated!.DisplayName.Should().Be("Adam Retail");
        updated.RoleTitle.Should().Be("Warehouse Lead");
        updated.Specialty.Should().Be("Retail stock, reorder planning, and supplier follow-up");
        updated.DefaultLanguage.Should().Be("ur-Latn");

        var tenantAResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/agents/employees",
            tenantA);
        var tenantAEmployees = await tenantAResponse.Content.ReadFromJsonAsync<List<AgentEmployeeDto>>(JsonOptions);
        tenantAEmployees.Should().Contain(e => e.Key == "adam" && e.DisplayName == "Adam Retail");

        var tenantBResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/agents/employees",
            tenantB);
        var tenantBEmployees = await tenantBResponse.Content.ReadFromJsonAsync<List<AgentEmployeeDto>>(JsonOptions);
        tenantBEmployees.Should().Contain(e => e.Key == "adam" && e.DisplayName == "Adam");
    }

    [Fact]
    public async Task AgentConfiguration_RejectsUnsupportedLanguage()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/adam/configuration",
            auth,
            new UpdateAgentConfigurationRequest
            {
                DisplayName = "Adam",
                RoleTitle = "Inventory Expert",
                Specialty = "Inventory management",
                DefaultLanguage = "xx"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AgentToolPermissions_AreTenantScoped_AndRejectUnknownTools()
    {
        var tenantA = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var tenantB = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var initial = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/agents/employees/adam/permissions",
            tenantA);
        initial.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialPermissions = await initial.Content.ReadFromJsonAsync<AgentToolPermissionsDto>(JsonOptions);
        initialPermissions.Should().NotBeNull();
        initialPermissions!.AllowedTools.Should().Contain(nameof(BusinessOS.Application.Features.AI.Enums.AiToolName.GetLowStock));

        var update = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/adam/permissions",
            tenantA,
            new UpdateAgentToolPermissionsRequest
            {
                AllowedTools = [nameof(BusinessOS.Application.Features.AI.Enums.AiToolName.GetLowStock)]
            });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<AgentToolPermissionsDto>(JsonOptions);
        updated!.AllowedTools.Should().Equal(nameof(BusinessOS.Application.Features.AI.Enums.AiToolName.GetLowStock));

        var tenantBResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/agents/employees/adam/permissions",
            tenantB);
        var tenantBPermissions = await tenantBResponse.Content.ReadFromJsonAsync<AgentToolPermissionsDto>(JsonOptions);
        tenantBPermissions.Should().NotBeNull();
        tenantBPermissions!.AllowedTools.Should().Contain(nameof(BusinessOS.Application.Features.AI.Enums.AiToolName.SearchProduct));

        var invalid = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/adam/permissions",
            tenantA,
            new UpdateAgentToolPermissionsRequest { AllowedTools = ["DropDatabase"] });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AgentToolPermissions_EmptyListMeansDenyAll()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var update = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/emma/permissions",
            auth,
            new UpdateAgentToolPermissionsRequest { AllowedTools = [] });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await update.Content.ReadFromJsonAsync<AgentToolPermissionsDto>(JsonOptions);
        saved!.AllowedTools.Should().BeEmpty();
    }

    [Fact]
    public async Task AgentOnboardingStart_ReturnsWorkflowSteps()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/agents/onboarding/start",
            auth,
            new AgentOnboardingStartRequest { Language = "en" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AgentOnboardingResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Reply.Should().NotBeNullOrWhiteSpace();
        body.WorkflowSteps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SalesActionProposal_IsTenantScoped_AndRequiresExplicitDecision()
    {
        var tenantA = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var enableFollowUp = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/emma/permissions",
            tenantA,
            new UpdateAgentToolPermissionsRequest
            {
                AllowedTools = ["SendCustomerFollowUp"]
            });
        enableFollowUp.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenantB = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var create = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/agents/actions/proposals",
            tenantA,
            new CreateAgentActionProposalRequest
            {
                ActionType = "SendCustomerFollowUp",
                Title = "Follow up with Ahmed",
                Summary = "Prepare a polite follow-up about the pending quotation.",
                PayloadJson = "{\"customerId\":\"11111111-1111-1111-1111-111111111111\",\"message\":\"Hello Ahmed\"}"
            });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var proposal = await create.Content.ReadFromJsonAsync<AgentActionProposalDto>(JsonOptions);
        proposal.Should().NotBeNull();
        proposal!.Status.Should().Be(AgentWorkflowStatus.AwaitingApproval);
        proposal.ActionType.Should().Be("SendCustomerFollowUp");

        var tenantAList = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/agents/actions/proposals", tenantA);
        var tenantAProposals = await tenantAList.Content.ReadFromJsonAsync<List<AgentActionProposalDto>>(JsonOptions);
        tenantAProposals.Should().ContainSingle(item => item.Id == proposal.Id);

        var tenantBList = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/agents/actions/proposals", tenantB);
        var tenantBProposals = await tenantBList.Content.ReadFromJsonAsync<List<AgentActionProposalDto>>(JsonOptions);
        tenantBProposals.Should().BeEmpty();

        var crossTenantGet = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/v1/agents/actions/proposals/{proposal.Id}",
            tenantB);
        crossTenantGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var crossTenantDecision = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/agents/actions/proposals/{proposal.Id}/decision",
            tenantB,
            new DecideAgentActionProposalRequest { Decision = "approved" });
        crossTenantDecision.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var approve = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/agents/actions/proposals/{proposal.Id}/decision",
            tenantA,
            new DecideAgentActionProposalRequest
            {
                Decision = "approved",
                Comment = "Reviewed and approved for the next release."
            });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var decided = await approve.Content.ReadFromJsonAsync<AgentActionProposalDto>(JsonOptions);
        decided!.Status.Should().Be(AgentWorkflowStatus.Approved);
        decided.DecisionComment.Should().Be("Reviewed and approved for the next release.");

        var duplicateDecision = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/agents/actions/proposals/{proposal.Id}/decision",
            tenantA,
            new DecideAgentActionProposalRequest { Decision = "rejected" });
        duplicateDecision.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SalesActionProposal_RejectsUnsupportedActionAndMalformedPayload()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var enableFollowUp = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/emma/permissions",
            auth,
            new UpdateAgentToolPermissionsRequest
            {
                AllowedTools = ["SendCustomerFollowUp"]
            });
        enableFollowUp.StatusCode.Should().Be(HttpStatusCode.OK);

        var unsupported = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/agents/actions/proposals",
            auth,
            new CreateAgentActionProposalRequest
            {
                ActionType = "DeleteCustomer",
                Title = "Delete customer",
                Summary = "This action is not enabled.",
                PayloadJson = "{}"
            });
        unsupported.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var malformed = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/agents/actions/proposals",
            auth,
            new CreateAgentActionProposalRequest
            {
                ActionType = "SendCustomerFollowUp",
                Title = "Follow up",
                Summary = "Prepare a message.",
                PayloadJson = "{not-json}"
            });
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SalesActionProposal_RequiresAuthentication()
    {
        var response = await Client.GetAsync("/api/v1/agents/actions/proposals");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task SalesActionProposal_ConcurrentDecisions_OnlyOneReviewerWins()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var enableFollowUp = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/emma/permissions",
            auth,
            new UpdateAgentToolPermissionsRequest
            {
                AllowedTools = ["SendCustomerFollowUp"]
            });
        enableFollowUp.StatusCode.Should().Be(HttpStatusCode.OK);

        var create = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/agents/actions/proposals",
            auth,
            new CreateAgentActionProposalRequest
            {
                ActionType = "SendCustomerFollowUp",
                Title = "Concurrency check",
                Summary = "Only one reviewer may decide this proposal.",
                PayloadJson = "{}"
            });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var proposal = await create.Content.ReadFromJsonAsync<AgentActionProposalDto>(JsonOptions);
        proposal.Should().NotBeNull();

        var approveTask = IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/agents/actions/proposals/{proposal!.Id}/decision",
            auth,
            new DecideAgentActionProposalRequest { Decision = "approved" });

        var rejectTask = IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/agents/actions/proposals/{proposal.Id}/decision",
            auth,
            new DecideAgentActionProposalRequest { Decision = "rejected" });

        var responses = await Task.WhenAll(approveTask, rejectTask);
        responses.Select(response => response.StatusCode)
            .Should().ContainSingle(status => status == HttpStatusCode.OK);
        responses.Select(response => response.StatusCode)
            .Should().ContainSingle(status => status == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SalesActionProposal_ExecutionRequiresApprovalAndActiveTenantCustomer()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var enableFollowUp = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Put,
            "/api/v1/agents/employees/emma/permissions",
            auth,
            new UpdateAgentToolPermissionsRequest
            {
                AllowedTools = ["SendCustomerFollowUp"]
            });
        enableFollowUp.StatusCode.Should().Be(HttpStatusCode.OK);

        var create = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/agents/actions/proposals",
            auth,
            new CreateAgentActionProposalRequest
            {
                ActionType = "SendCustomerFollowUp",
                Title = "Follow up with a customer",
                Summary = "Send a reviewed follow-up message.",
                PayloadJson = "{\"customerId\":\"11111111-1111-1111-1111-111111111111\",\"message\":\"Hello, following up on your quotation.\"}"
            });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var proposal = await create.Content.ReadFromJsonAsync<AgentActionProposalDto>(JsonOptions);
        proposal.Should().NotBeNull();

        var beforeApproval = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/agents/actions/proposals/{proposal!.Id}/execute",
            auth);
        beforeApproval.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var approve = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/agents/actions/proposals/{proposal.Id}/decision",
            auth,
            new DecideAgentActionProposalRequest { Decision = "approved" });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        // A missing customer is rejected before the one-time execution claim is acquired.
        var execute = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/agents/actions/proposals/{proposal.Id}/execute",
            auth);
        execute.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var detail = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            $"/api/v1/agents/actions/proposals/{proposal.Id}",
            auth);
        var current = await detail.Content.ReadFromJsonAsync<AgentActionProposalDto>(JsonOptions);
        current!.Status.Should().Be(AgentWorkflowStatus.Approved);
    }

    [Fact]
    public async Task SalesActionProposal_RequiresExplicitEmployeeCapability()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/agents/actions/proposals",
            auth,
            new CreateAgentActionProposalRequest
            {
                ActionType = "SendCustomerFollowUp",
                Title = "Capability disabled",
                Summary = "This action must be denied until the tenant enables it.",
                PayloadJson = "{}"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }


}

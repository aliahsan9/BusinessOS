using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Nalva;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class NalvaPhase5WorkforceTests
{
    [Fact]
    public void WorkforcePacks_CoverBusinessTypesWithoutIrrelevantForcedAgents()
    {
        NalvaWorkforcePackCatalog.All.Should().NotBeEmpty();
        NalvaWorkforcePackCatalog.Resolve("software-agency").RecommendedAgentKeys.Should().Contain(["sales", "software", "finance"]);
        NalvaWorkforcePackCatalog.Resolve("software-agency").RecommendedAgentKeys.Should().NotContain("procurement");
        NalvaWorkforcePackCatalog.Resolve("retail").RecommendedAgentKeys.Should().Contain(["procurement", "sales"]);
        NalvaWorkforcePackCatalog.Resolve("marketing-agency").RecommendedAgentKeys.Should().Contain(["social", "content", "email"]);
        NalvaWorkforcePackCatalog.Resolve("distributor").RecommendedAgentKeys.Should().Contain("procurement");
        NalvaWorkforcePackCatalog.Resolve("unknown-widget-shop").BusinessTypeKey.Should().Be("custom");

        foreach (var pack in NalvaWorkforcePackCatalog.All)
        {
            foreach (var agent in pack.RecommendedAgentKeys)
                NalvaAgentCatalog.TryGet(agent, out _).Should().BeTrue(because: $"{pack.BusinessTypeKey}:{agent}");
            foreach (var workflow in pack.RecommendedWorkflowKeys)
                NalvaWorkflowCatalog.TryGet(workflow, out _).Should().BeTrue(because: $"{pack.BusinessTypeKey}:{workflow}");
        }
    }

    [Fact]
    public void OverduePaymentWorkflow_IsPresentAndStopsAtExternalSend()
    {
        NalvaWorkflowCatalog.TryGet("overdue-payment-followup", out var template).Should().BeTrue();
        template!.TriggerEvent.Should().Be(NalvaEventNames.InvoiceOverdue);
        template.Steps.SelectMany(x => x.Tools).Should().Contain("SendExternalEmail");
        template.Steps.SelectMany(x => x.Tools).Should().Contain("GetOutstandingReceivables");
    }

    [Fact]
    public void MetaCatalog_ExposesApprovedPublishBoundary()
    {
        IntegrationProviderCatalog.TryGet("meta", out var meta).Should().BeTrue();
        meta!.WriteOperations.Should().Contain("publish");
        meta.ProviderVerified.Should().BeFalse();
        IntegrationProviderCatalog.TryGet("linkedin", out var linkedIn).Should().BeTrue();
        linkedIn!.WriteOperations.Should().BeEmpty();
    }

    [Fact]
    public async Task SalesLeadGeneration_CreatesLeadAndStopsBeforeExternalSend()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("sales");
        await harness.Service.ActivateAsync("marketing");
        await harness.Service.ActivateAsync("ceo");

        var lead = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "sales",
            Objective = "Create a qualified software-agency prospect.",
            Tools = ["CreateLead", "FindDuplicateLeads", "CreateEmailDraft", "SendExternalEmail"],
            ArgumentsJson = "{\"name\":\"Northwind Labs\",\"email\":\"buyer@northwind.example\",\"company\":\"Northwind Labs\"}"
        });

        lead.Status.Should().Be(NalvaStatuses.AwaitingApproval);
        harness.Db.Leads.Should().ContainSingle(x => x.Email == "buyer@northwind.example");
        harness.Db.NalvaWorkItems.Should().Contain(x => x.Kind == "Email");
        harness.Db.NalvaApprovals.Should().ContainSingle(x => x.Action == "SendExternalEmail");

        var workflows = await harness.Service.GetWorkflowsAsync();
        var acquisition = workflows.Single(x => x.Key == "customer-acquisition");
        await harness.Service.SetWorkflowActiveAsync(acquisition.Id, true);
        var run = await harness.Service.RunWorkflowAsync(acquisition.Id, "{}");
        run.Status.Should().Be(NalvaStatuses.AwaitingApproval);
    }

    [Fact]
    public async Task MarketingSocialCampaign_ReachesApprovalWithoutPublishing()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("marketing");
        await harness.Service.ActivateAsync("social");
        var workflows = await harness.Service.GetWorkflowsAsync();
        var campaign = workflows.Single(x => x.Key == "social-campaign");
        await harness.Service.SetWorkflowActiveAsync(campaign.Id, true);

        var run = await harness.Service.RunWorkflowAsync(campaign.Id, "{\"brief\":\"Launch week for agency services\"}");

        run.Status.Should().Be(NalvaStatuses.AwaitingApproval);
        harness.Db.NalvaWorkItems.Should().Contain(x => x.Kind == "Campaign" || x.Kind == "Calendar" || x.Kind == "Strategy" || x.Kind == "Content" || x.Kind == "Graphic");
        harness.Db.NalvaApprovals.Should().ContainSingle(x => x.Action == "PublishSocialContent");

        var approval = harness.Db.NalvaApprovals.Single(x => x.Action == "PublishSocialContent");
        var decided = await harness.Service.DecideApprovalAsync(approval.Id, "approve", "Preview OK");
        (decided.ResultJson ?? decided.Summary ?? "").Should().MatchRegex("(?i)performed|not publish|no external");
    }

    [Fact]
    public async Task NavlaMarketingCampaign_Workflow_StopsAtPublishApproval()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("ceo");
        await harness.Service.ActivateAsync("sales");
        await harness.Service.ActivateAsync("marketing");
        await harness.Service.ActivateAsync("social");

        NalvaWorkflowCatalog.TryGet("navla-marketing-campaign", out var template).Should().BeTrue();
        template!.Steps.SelectMany(x => x.Tools).Should().Contain("AssignMarketingObjective");
        template.Steps.SelectMany(x => x.Tools).Should().Contain("PublishSocialPost");

        var workflows = await harness.Service.GetWorkflowsAsync();
        var campaign = workflows.Single(x => x.Key == "navla-marketing-campaign");
        await harness.Service.SetWorkflowActiveAsync(campaign.Id, true);

        var run = await harness.Service.RunWorkflowAsync(campaign.Id, "{\"title\":\"Q1 awareness\",\"objective\":\"BrandAwareness\",\"brief\":\"Collaborate across CEO, sales, and marketing\"}");

        run.Status.Should().Be(NalvaStatuses.AwaitingApproval);
        harness.Db.NalvaApprovals.Should().Contain(x => x.Action == "PublishSocialContent");
        harness.Db.NalvaWorkItems.Should().Contain(x =>
            x.Kind == "MarketingObjective" || x.Kind == "Campaign" || x.Kind == "Calendar" || x.Kind == "Strategy" || x.Kind == "Content" || x.Kind == "Graphic");
    }

    [Fact]
    public async Task WorkforcePack_AppliesRecommendedAgentsForBusinessType()
    {
        var harness = Harness.Open(businessType: "software-agency");
        var recommendation = await harness.Service.GetWorkforceRecommendationAsync();
        recommendation.PackKey.Should().Be("software-agency");
        recommendation.Agents.Count(x => x.IsRecommended).Should().BeGreaterThan(3);

        var applied = await harness.Service.ApplyWorkforcePackAsync(new NalvaApplyWorkforcePackRequest
        {
            ActivateRecommendedAgents = true,
            ActivateRecommendedWorkflows = false,
            OnlyAgentKeys = ["sales", "software", "finance"]
        });

        applied.ActivatedAgents.Should().BeEquivalentTo(["sales", "software", "finance"]);
        (await harness.Service.GetMyAgentsAsync()).Select(x => x.Key).Should().Contain(["sales", "software", "finance"]);
    }

    [Fact]
    public async Task Outcomes_TrackRealLeadAndDraftWork()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("sales");
        await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "sales",
            Objective = "Create lead",
            Tools = ["CreateLead"],
            ArgumentsJson = "{\"name\":\"Ada\",\"email\":\"ada@example.com\"}"
        });
        await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "sales",
            Objective = "Draft outreach",
            Tools = ["CreateEmailDraft"],
            ArgumentsJson = "{\"subject\":\"Hello\",\"body\":\"Intro\"}"
        });

        var outcomes = await harness.Service.GetOutcomesAsync("sales", null, null);
        var sales = outcomes.Single(x => x.AgentKey == "sales");
        sales.LeadsCreated.Should().BeGreaterThan(0);
        sales.EmailDrafts.Should().BeGreaterThan(0);
        sales.CostIsEstimate.Should().BeTrue();
    }

    [Fact]
    public async Task OverduePaymentWorkflow_ReachesApproval()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("finance");
        await harness.Service.ActivateAsync("sales");
        await harness.Service.ActivateAsync("ceo");
        var workflows = await harness.Service.GetWorkflowsAsync();
        var overdue = workflows.Single(x => x.Key == "overdue-payment-followup");
        await harness.Service.SetWorkflowActiveAsync(overdue.Id, true);

        var run = await harness.Service.RunWorkflowAsync(overdue.Id, "{\"invoiceId\":\"00000000-0000-0000-0000-000000000001\"}");

        run.Status.Should().Be(NalvaStatuses.AwaitingApproval);
        harness.Db.NalvaApprovals.Should().Contain(x => x.Action == "SendExternalEmail");
    }

    private sealed class Harness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaExecutionEngine Engine { get; init; }

        public static Harness Open(string? database = null, string? businessType = null)
        {
            var (db, tenantId, provider) = TenantDatabase.Open(name: database);
            if (!string.IsNullOrWhiteSpace(businessType))
            {
                db.Tenants.Add(new Tenant
                {
                    Id = tenantId,
                    Name = "Phase5 Co",
                    Slug = "phase5-co",
                    Email = "owner@phase5.example",
                    Phone = "555",
                    Address = "Office",
                    Country = "Pakistan",
                    BusinessType = businessType,
                    OwnerUserId = "user-1",
                    SubscriptionPlanId = Guid.NewGuid(),
                    Status = TenantStatus.Active,
                    IsActive = true
                });
                db.SaveChanges();
            }

            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
            var gateway = new NalvaToolGateway(db, provider, new CrmOperationsService(db, user.Object));
            var engine = new NalvaExecutionEngine(db, provider, user.Object, gateway, new SilentModel(), NullLogger<NalvaExecutionEngine>.Instance);
            var service = new NalvaWorkforceService(db, user.Object, engine, new NalvaWorkflowEngine(db, engine));
            return new Harness { Db = db, Service = service, Engine = engine };
        }
    }

    private sealed class SilentModel : ILlmChatClient
    {
        public bool IsConfigured => false;
        public Task<string?> GenerateReplyAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) => Task.FromResult<string?>(null);
        public async IAsyncEnumerable<string> StreamReplyAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) { await Task.CompletedTask; yield break; }
        public Task<(string? Reply, int? TokenUsage)> GenerateWithToolsAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, IReadOnlyList<object> toolResults, CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) => Task.FromResult<(string?, int?)>((null, null));
    }
}

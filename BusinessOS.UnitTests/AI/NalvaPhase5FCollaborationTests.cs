using System.Text.Json.Nodes;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
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

public class NalvaPhase5FCollaborationTests
{
    [Fact]
    public void CollaborationWorkflows_AreRegisteredWithValidatedAgentTools()
    {
        string[] keys =
        [
            "lead-to-customer-collaboration",
            "order-to-fulfillment",
            "low-stock-to-procurement",
            "complaint-to-resolution",
            "weekly-business-review"
        ];

        foreach (var key in keys)
        {
            NalvaWorkflowCatalog.TryGet(key, out var template).Should().BeTrue(because: key);
            template!.Steps.Should().NotBeEmpty();
            foreach (var step in template.Steps)
            {
                NalvaAgentCatalog.TryGet(step.AgentKey, out var agent).Should().BeTrue($"{key}:{step.Key}:{step.AgentKey}");
                foreach (var tool in step.Tools)
                {
                    agent!.Tools.Should().Contain(tool, because: $"{key}/{step.Key} tool {tool} must be granted to {step.AgentKey}");
                    NalvaToolCatalog.TryGet(tool, out _).Should().BeTrue();
                }
            }
        }
    }

    [Fact]
    public void AgentCommunicationProtocol_ValidatesTaskAndResultEnvelopes()
    {
        var task = new NalvaAgentTaskEnvelope
        {
            TaskId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AssignedAgent = "sales",
            Objective = "Qualify researched prospects",
            Permissions = ["QualifyLead"],
            RetryPolicy = new NalvaRetryPolicy { MaxAttempts = 3 }
        };
        NalvaCollaborationProtocol.TryValidateTask(task, out var taskError).Should().BeTrue(taskError);

        var badTask = new NalvaAgentTaskEnvelope
        {
            TaskId = task.TaskId,
            TenantId = task.TenantId,
            AssignedAgent = "not-an-agent",
            Objective = task.Objective
        };
        NalvaCollaborationProtocol.TryValidateTask(badTask, out _).Should().BeFalse();

        var completed = new NalvaAgentResultEnvelope
        {
            TaskId = task.TaskId,
            ExecutionId = Guid.NewGuid(),
            AgentKey = "sales",
            Status = NalvaAgentResultStatus.Completed,
            Verified = true,
            Summary = "Qualified from CRM records."
        };
        NalvaCollaborationProtocol.TryValidateResult(completed, out var resultError).Should().BeTrue(resultError);

        var unverifiedComplete = new NalvaAgentResultEnvelope
        {
            TaskId = completed.TaskId,
            ExecutionId = completed.ExecutionId,
            AgentKey = completed.AgentKey,
            Status = NalvaAgentResultStatus.Completed,
            Verified = false,
            Summary = completed.Summary
        };
        NalvaCollaborationProtocol.TryValidateResult(unverifiedComplete, out _).Should().BeFalse();

        var untrustedPeer = new NalvaAgentResultEnvelope
        {
            TaskId = completed.TaskId,
            ExecutionId = completed.ExecutionId,
            AgentKey = "sales",
            Status = NalvaAgentResultStatus.PartiallyCompleted,
            Verified = false,
            Summary = "Partial"
        };
        var trust = NalvaCollaborationProtocol.TrustBoundary(untrustedPeer);
        trust["untrusted"]!.GetValue<bool>().Should().BeTrue();
        trust.ContainsKey("results").Should().BeFalse();
    }

    [Fact]
    public void ObjectiveRouter_MapsNaturalLanguageToWorkflows()
    {
        NalvaObjectiveRouter.Match("Find 50 qualified leads for my software agency and prepare outreach")
            .WorkflowKey.Should().Be("lead-to-customer-collaboration");
        NalvaObjectiveRouter.Match("Analyze our low-stock products and prepare procurement recommendations")
            .WorkflowKey.Should().Be("low-stock-to-procurement");
        NalvaObjectiveRouter.Match("Review all unresolved customer complaints and coordinate resolutions")
            .WorkflowKey.Should().Be("complaint-to-resolution");
        NalvaObjectiveRouter.Match("Prepare a weekly business performance report")
            .WorkflowKey.Should().Be("weekly-business-review");
        NalvaObjectiveRouter.Match("Check our outstanding invoices and prepare customer reminders")
            .WorkflowKey.Should().Be("overdue-payment-followup");
        NalvaObjectiveRouter.Match("Coordinate the complete order fulfillment process")
            .WorkflowKey.Should().Be("order-to-fulfillment");

        var ambiguous = NalvaObjectiveRouter.Match("Do something useful");
        ambiguous.CanStart.Should().BeFalse();
        ambiguous.ClarifyingQuestions.Should().NotBeEmpty();
    }

    [Fact]
    public void CollaborationRules_PreventPermissionEscalationAndSelfApproval()
    {
        NalvaCollaborationProtocol.AgentsMayEscalatePermissions("sales", "finance").Should().BeFalse();
        NalvaCollaborationProtocol.MaySelfApprove("finance", "IssueRefund").Should().BeFalse();
    }

    [Fact]
    public async Task SequentialAndParallelWorkflows_PersistAndPauseForApproval()
    {
        var harness = Harness.Open();
        foreach (var agent in new[] { "ceo", "sales", "marketing", "support", "finance", "procurement", "analyst", "ecommerce" })
            await harness.Service.ActivateAsync(agent);

        var plan = await harness.Collab.PlanObjectiveAsync(new NalvaObjectivePlanRequest
        {
            Objective = "Find qualified leads and prepare outreach"
        });
        plan.WorkflowKey.Should().Be("lead-to-customer-collaboration");
        plan.Steps.Should().NotBeEmpty();
        plan.CanStart.Should().BeTrue(plan.BlockReason);

        var started = await harness.Collab.StartObjectiveAsync(new NalvaObjectivePlanRequest
        {
            Objective = "Find qualified leads and prepare outreach",
            ActivateMissingAgents = true,
            ActivateWorkflow = true,
            IdempotencyKey = "phase5f-lead-1"
        });
        started.WorkflowRunId.Should().NotBeNull();
        started.RunStatus.Should().BeOneOf(NalvaStatuses.AwaitingApproval, NalvaStatuses.Completed, NalvaStatuses.Failed, NalvaStatuses.Running);

        var board = await harness.Collab.GetWorkforceBoardAsync();
        board.CollaborationWorkflows.Should().Contain(x => x.Key == "lead-to-customer-collaboration");
        board.Agents.Should().NotBeEmpty();

        var detail = await harness.Collab.GetWorkflowDetailAsync(started.WorkflowRunId!.Value);
        detail.Run.Id.Should().Be(started.WorkflowRunId.Value);
        detail.FinalReport.Should().NotBeNullOrWhiteSpace();
        detail.AgentResults.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WeeklyBusinessReview_RunsParallelCollectionSteps()
    {
        var harness = Harness.Open();
        foreach (var agent in new[] { "ceo", "sales", "marketing", "support", "finance", "procurement", "analyst" })
            await harness.Service.ActivateAsync(agent);

        var started = await harness.Collab.StartObjectiveAsync(new NalvaObjectivePlanRequest
        {
            Objective = "Prepare a weekly business performance report",
            ActivateMissingAgents = true,
            ActivateWorkflow = true,
            IdempotencyKey = "phase5f-weekly-1"
        });

        started.WorkflowKey.Should().Be("weekly-business-review");
        started.WorkflowRunId.Should().NotBeNull();
        var detail = await harness.Collab.GetWorkflowDetailAsync(started.WorkflowRunId!.Value);
        detail.Steps.Count(x => x.ParallelGroup == 1).Should().BeGreaterThan(1);
        detail.FinalReport.Should().Contain("status=");
    }

    [Fact]
    public async Task LowStockAndComplaintWorkflows_AreStartable()
    {
        var harness = Harness.Open();
        foreach (var agent in new[] { "ceo", "sales", "support", "finance", "procurement" })
            await harness.Service.ActivateAsync(agent);

        var stock = await harness.Collab.StartObjectiveAsync(new NalvaObjectivePlanRequest
        {
            Objective = "Analyze our low-stock products and prepare procurement recommendations",
            ActivateMissingAgents = true,
            ActivateWorkflow = true,
            IdempotencyKey = "phase5f-stock-1"
        });
        stock.WorkflowKey.Should().Be("low-stock-to-procurement");
        stock.WorkflowRunId.Should().NotBeNull();

        var complaint = await harness.Collab.StartObjectiveAsync(new NalvaObjectivePlanRequest
        {
            Objective = "Review unresolved customer complaints and coordinate resolutions",
            ActivateMissingAgents = true,
            ActivateWorkflow = true,
            ArgumentsJson = "{\"complaint\":\"Package arrived damaged and I want a refund\"}",
            IdempotencyKey = "phase5f-complaint-1"
        });
        complaint.WorkflowKey.Should().Be("complaint-to-resolution");
        complaint.WorkflowRunId.Should().NotBeNull();
    }

    [Fact]
    public async Task SharedContext_IsTenantScopedAndRetrievable()
    {
        var harness = Harness.Open();
        var stored = await harness.Collab.StoreSharedDecisionAsync(
            "Outreach policy",
            "No external email without owner approval.",
            null);
        stored.Kind.Should().Be("BusinessDecision");

        var list = await harness.Collab.GetSharedContextAsync("BusinessDecision");
        list.Should().Contain(x => x.Id == stored.Id);
    }

    [Fact]
    public async Task ApprovalDto_IncludesWorkflowContextAndFingerprint()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("sales");
        var result = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "sales",
            Objective = "Create a lead and prepare outreach send",
            Tools = ["CreateLead", "CreateEmailDraft", "SendExternalEmail"],
            ArgumentsJson = "{\"name\":\"Phase5F Co\",\"email\":\"buyer@phase5f.example\",\"company\":\"Phase5F Co\"}"
        });
        result.Status.Should().Be(NalvaStatuses.AwaitingApproval);

        var approvals = await harness.Service.GetApprovalsAsync(NalvaStatuses.Pending);
        approvals.Should().NotBeEmpty();
        var approval = approvals.First(x => x.Action == "SendExternalEmail" || x.ToolName == "SendExternalEmail");
        approval.ArgsFingerprint.Should().NotBeNullOrWhiteSpace();
        approval.TaskObjective.Should().NotBeNullOrWhiteSpace();
        approval.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CrossTenant_WorkflowDetailIsIsolated()
    {
        var a = Harness.Open(database: "phase5f-a");
        var b = Harness.Open(database: "phase5f-b");
        foreach (var agent in new[] { "ceo", "sales", "marketing", "support", "finance" })
            await a.Service.ActivateAsync(agent);

        var started = await a.Collab.StartObjectiveAsync(new NalvaObjectivePlanRequest
        {
            Objective = "Prepare a weekly business performance report",
            ActivateMissingAgents = true,
            ActivateWorkflow = true,
            IdempotencyKey = "phase5f-tenant-a"
        });

        var act = async () => await b.Collab.GetWorkflowDetailAsync(started.WorkflowRunId!.Value);
        await act.Should().ThrowAsync<Exception>();
    }

    private sealed class Harness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaExecutionEngine Engine { get; init; }
        public required NalvaCollaborationService Collab { get; init; }

        public static Harness Open(string? database = null)
        {
            var (db, tenantId, provider) = TenantDatabase.Open(name: database);
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
            var gateway = new NalvaToolGateway(db, provider, new CrmOperationsService(db, user.Object));
            var engine = new NalvaExecutionEngine(db, provider, user.Object, gateway, new SilentModel(), NullLogger<NalvaExecutionEngine>.Instance);
            var workflows = new NalvaWorkflowEngine(db, engine);
            var service = new NalvaWorkforceService(db, user.Object, engine, workflows);
            var collab = new NalvaCollaborationService(db, user.Object, provider, service, workflows);
            return new Harness { Db = db, Service = service, Engine = engine, Collab = collab };
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

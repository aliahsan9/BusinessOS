using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.AI.Nalva;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class NalvaPhase5GReliabilityTests
{
    [Fact]
    public async Task StalledRunningTask_WithoutExecution_IsRequeued()
    {
        var harness = Harness.Open();
        var task = new NalvaTask
        {
            AgentKey = "sales",
            Objective = "Recover me",
            Status = NalvaStatuses.Running,
            ClaimToken = Guid.NewGuid(),
            Attempt = 0,
            MaxAttempts = 3,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        harness.Db.NalvaTasks.Add(task);
        await harness.Db.SaveChangesAsync();

        var result = await harness.Reliability.RecoverStalledWorkAsync();

        result.TasksRecovered.Should().Be(1);
        var reloaded = await harness.Db.NalvaTasks.SingleAsync(x => x.Id == task.Id);
        reloaded.Status.Should().BeOneOf(NalvaStatuses.Assigned, NalvaStatuses.Queued);
        reloaded.ClaimToken.Should().BeNull();
        reloaded.Attempt.Should().Be(1);
        reloaded.ErrorClass.Should().Be(NalvaErrorClass.Transient);
    }

    [Fact]
    public async Task StalledRunningTask_WithOpenExecution_GoesToDeadLetter()
    {
        var harness = Harness.Open();
        var task = new NalvaTask
        {
            AgentKey = "sales",
            Objective = "Uncertain recovery",
            Status = NalvaStatuses.Running,
            ClaimToken = Guid.NewGuid(),
            Attempt = 0,
            MaxAttempts = 3,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        harness.Db.NalvaTasks.Add(task);
        await harness.Db.SaveChangesAsync();
        harness.Db.NalvaExecutions.Add(new NalvaExecution
        {
            AgentKey = "sales",
            TaskId = task.Id,
            Objective = "Uncertain recovery",
            Status = NalvaStatuses.Running,
            Model = "gpt-4o-mini",
            PricingVersion = NalvaLimits.PricingVersion,
            StartedAt = DateTime.UtcNow.AddMinutes(-10)
        });
        await harness.Db.SaveChangesAsync();

        var result = await harness.Reliability.RecoverStalledWorkAsync();

        result.TasksRecovered.Should().Be(1);
        result.ExecutionsClosed.Should().Be(1);
        var reloaded = await harness.Db.NalvaTasks.SingleAsync(x => x.Id == task.Id);
        reloaded.Status.Should().Be(NalvaStatuses.DeadLetter);
        reloaded.ErrorClass.Should().Be(NalvaErrorClass.Uncertain);
    }

    [Fact]
    public async Task ExpiredPendingApproval_IsMarkedExpired()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("sales");
        var execution = new NalvaExecution
        {
            AgentKey = "sales",
            Objective = "Send mail",
            Status = NalvaStatuses.AwaitingApproval,
            Model = "gpt-4o-mini",
            PricingVersion = NalvaLimits.PricingVersion,
            StartedAt = DateTime.UtcNow.AddDays(-8)
        };
        harness.Db.NalvaExecutions.Add(execution);
        await harness.Db.SaveChangesAsync();
        harness.Db.NalvaApprovals.Add(new NalvaApproval
        {
            ExecutionId = execution.Id,
            AgentKey = "sales",
            Action = "SendExternalEmail",
            ToolName = "SendExternalEmail",
            Status = NalvaStatuses.Pending,
            ArgsJson = "{\"body\":\"hi\"}",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        await harness.Db.SaveChangesAsync();

        var result = await harness.Reliability.RecoverStalledWorkAsync();

        result.ApprovalsExpired.Should().Be(1);
        (await harness.Db.NalvaApprovals.SingleAsync()).Status.Should().Be(NalvaStatuses.Expired);
    }

    [Fact]
    public async Task Enqueue_WithSameIdempotencyKey_DoesNotDuplicateInFlightWork()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("sales");
        var first = await harness.Service.EnqueueAsync("sales", new NalvaRunRequest
        {
            Objective = "Research prospects",
            Tools = ["GetSalesPipeline"],
            IdempotencyKey = "sales-research-1"
        });
        var second = await harness.Service.EnqueueAsync("sales", new NalvaRunRequest
        {
            Objective = "Research prospects again",
            Tools = ["GetSalesPipeline"],
            IdempotencyKey = "sales-research-1"
        });

        second.Id.Should().Be(first.Id);
        harness.Db.NalvaTasks.Count(x => x.IdempotencyKey == "sales-research-1").Should().Be(1);
    }

    [Fact]
    public async Task ApprovalModify_UpdatesFingerprintAndArgs()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("sales");
        var waiting = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "sales",
            Objective = "Send the outreach.",
            Tools = ["SendExternalEmail"],
            ArgumentsJson = "{\"body\":\"Hello\"}"
        });
        waiting.ApprovalId.Should().NotBeNull();

        var modified = await harness.Service.DecideApprovalAsync(
            waiting.ApprovalId!.Value,
            "changes",
            "tighten wording",
            "{\"body\":\"Hello — revised\"}");

        modified.Status.Should().Be(NalvaStatuses.AwaitingApproval);
        var approval = await harness.Db.NalvaApprovals.SingleAsync(x => x.Id == waiting.ApprovalId);
        approval.Status.Should().Be(NalvaStatuses.ChangesRequested);
        approval.ArgsJson.Should().Contain("revised");
        approval.ContextJson.Should().Contain("argsFingerprint");
    }

    [Fact]
    public async Task ReliabilitySnapshot_IsTenantScoped()
    {
        var database = Guid.NewGuid().ToString("N");
        var left = Harness.Open(database);
        var right = Harness.Open(database);
        left.Db.NalvaTasks.Add(new NalvaTask
        {
            AgentKey = "sales",
            Objective = "left",
            Status = NalvaStatuses.DeadLetter,
            CreatedAt = DateTime.UtcNow
        });
        await left.Db.SaveChangesAsync();
        right.Db.NalvaTasks.Add(new NalvaTask
        {
            AgentKey = "sales",
            Objective = "right",
            Status = NalvaStatuses.DeadLetter,
            CreatedAt = DateTime.UtcNow
        });
        await right.Db.SaveChangesAsync();

        var leftSnap = await left.Service.GetReliabilitySnapshotAsync();
        var rightSnap = await right.Service.GetReliabilitySnapshotAsync();

        leftSnap.DeadLetterTasks.Should().Be(1);
        rightSnap.DeadLetterTasks.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentClaim_OnlyOneWorkerRunsTheTask()
    {
        var database = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        var left = Harness.Open(database, tenantId);
        var right = Harness.Open(database, tenantId);
        await left.Service.ActivateAsync("sales");
        var task = await left.Service.EnqueueAsync("sales", new NalvaRunRequest
        {
            Objective = "Get the sales pipeline.",
            Tools = ["GetSalesPipeline"]
        });

        await Task.WhenAll(
            left.Engine.RunQueuedTaskAsync(task.Id),
            right.Engine.RunQueuedTaskAsync(task.Id));

        // Fresh context so we observe store state after both workers finish.
        var verify = Harness.Open(database, tenantId);
        verify.Db.NalvaExecutions.Count(x => x.TaskId == task.Id).Should().Be(1);
        var reloaded = await verify.Db.NalvaTasks.SingleAsync(x => x.Id == task.Id);
        reloaded.Status.Should().BeOneOf(NalvaStatuses.Completed, NalvaStatuses.Failed, NalvaStatuses.AwaitingApproval);
    }

    [Fact]
    public async Task AutomationDashboard_SurfacesPendingApprovalsAndDeadLetters()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("sales");
        harness.Db.NalvaTasks.Add(new NalvaTask
        {
            AgentKey = "sales",
            Objective = "dead",
            Status = NalvaStatuses.DeadLetter
        });
        await harness.Db.SaveChangesAsync();
        await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "sales",
            Objective = "Send the outreach.",
            Tools = ["SendExternalEmail"],
            ArgumentsJson = "{\"body\":\"Hello\"}"
        });

        var dashboard = await harness.Automation.GetDashboardAsync();
        dashboard.DeadLetterTasks.Should().BeGreaterThan(0);
        dashboard.PendingApprovals.Should().BeGreaterThan(0);
    }

    private sealed class Harness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaExecutionEngine Engine { get; init; }
        public required NalvaReliabilityService Reliability { get; init; }
        public required NalvaAutomationService Automation { get; init; }

        public static Harness Open(string? database = null, Guid? tenantId = null)
        {
            var (db, _, provider) = TenantDatabase.Open(tenantId: tenantId, name: database);
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(provider.TenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
            var audit = new EntityAuditService(db, user.Object);
            var finance = new FinanceAgentService(db, provider, user.Object, audit, NullLogger<FinanceAgentService>.Instance);
            var gateway = new NalvaToolGateway(db, provider, new CrmOperationsService(db, user.Object), finance: finance);
            var engine = new NalvaExecutionEngine(db, provider, user.Object, gateway, new SilentModel(), NullLogger<NalvaExecutionEngine>.Instance);
            var service = new NalvaWorkforceService(db, user.Object, engine, new NalvaWorkflowEngine(db, engine));
            var reliability = new NalvaReliabilityService(db, NullLogger<NalvaReliabilityService>.Instance);
            var automation = new NalvaAutomationService(db, user.Object, service);
            return new Harness
            {
                Db = db,
                Service = service,
                Engine = engine,
                Reliability = reliability,
                Automation = automation
            };
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

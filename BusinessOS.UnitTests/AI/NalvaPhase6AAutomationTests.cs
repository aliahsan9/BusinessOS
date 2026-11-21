using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
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

public class NalvaPhase6AAutomationTests
{
    [Fact]
    public void DailyRecurrence_FiresOncePerLocalDay()
    {
        var now = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
        var spec = new NalvaScheduleSpec("UTC", NalvaRecurrenceKinds.Daily, null, null, null, "09:00", null, null);
        NalvaSchedule.TryDue(spec, now, null, out var window, out _).Should().BeTrue();
        NalvaSchedule.TryDue(spec, now.AddHours(2), window, out _, out _).Should().BeFalse();
        NalvaSchedule.TryDue(spec, now.AddDays(1), window, out var next, out _).Should().BeTrue();
        next.Should().NotBe(window);
    }

    [Fact]
    public void WeeklyRecurrence_RespectsDaysOfWeekAndTimezone()
    {
        // Saturday 2026-11-21 UTC — Mon/Wed/Fri custom should wait until Monday.
        var saturday = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        var spec = new NalvaScheduleSpec("UTC", NalvaRecurrenceKinds.Weekly, null, null, null, "09:00", "Mon,Wed,Fri", null);
        NalvaSchedule.TryDue(spec, saturday, null, out _, out var next).Should().BeFalse();
        next.DayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void MonthlyRecurrence_UsesDayOfMonth()
    {
        var now = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
        var spec = new NalvaScheduleSpec("UTC", NalvaRecurrenceKinds.Monthly, null, null, null, "08:00", null, 1);
        // Past day-of-month with no last window: catch-up fire for November, next is December 1.
        NalvaSchedule.TryDue(spec, now, null, out var window, out var next).Should().BeTrue();
        window.Should().Be("m:2026-11");
        next.Month.Should().Be(12);
        next.Day.Should().Be(1);
        NalvaSchedule.TryDue(spec, now, window, out _, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Schedule_CanBePausedResumedAndCancelled()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("sales");
        var created = await harness.Automation.SaveScheduleAsync(new NalvaScheduleWriteRequest
        {
            Name = "Daily sales follow-up",
            RecurrenceKind = NalvaRecurrenceKinds.Daily,
            LocalTimeOfDay = "09:00",
            TimeZoneId = "UTC",
            Activate = true,
            Steps =
            [
                new NalvaWorkflowStepWrite
                {
                    Key = "scan",
                    AgentKey = "sales",
                    Objective = "Review overdue follow-ups and draft outreach.",
                    Tools = []
                }
            ]
        });

        created.IsActive.Should().BeTrue();
        created.IsPaused.Should().BeFalse();
        created.RecurrenceKind.Should().Be(NalvaRecurrenceKinds.Daily);

        var paused = await harness.Automation.PauseScheduleAsync(created.Id);
        paused.IsPaused.Should().BeTrue();

        var resumed = await harness.Automation.ResumeScheduleAsync(created.Id);
        resumed.IsPaused.Should().BeFalse();
        resumed.IsActive.Should().BeTrue();

        var cancelled = await harness.Automation.CancelScheduleAsync(created.Id);
        cancelled.IsCancelled.Should().BeTrue();
        cancelled.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task EventRule_RequiresRegisteredEventAndCanBeDisabled()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("support");
        var rule = await harness.Automation.SaveRuleAsync(new NalvaAutomationRuleWriteRequest
        {
            Name = "Support intake",
            TriggerKind = "Event",
            TriggerEvent = NalvaEventNames.SupportTicketCreated,
            Enable = true,
            Steps =
            [
                new NalvaWorkflowStepWrite
                {
                    Key = "intake",
                    AgentKey = "support",
                    Objective = "Prepare a response draft for the new ticket.",
                    Tools = []
                }
            ]
        });

        rule.IsEnabled.Should().BeTrue();
        rule.TriggerEvent.Should().Be(NalvaEventNames.SupportTicketCreated);

        var disabled = await harness.Automation.SetRuleEnabledAsync(rule.Id, false);
        disabled.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleWorker_SkipsWhenPausedAndHonorsConcurrency()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("finance");
        var schedule = await harness.Automation.SaveScheduleAsync(new NalvaScheduleWriteRequest
        {
            Name = "Finance monitor",
            RecurrenceKind = NalvaRecurrenceKinds.Once,
            Activate = true,
            MaxConcurrentRuns = 1,
            Steps =
            [
                new NalvaWorkflowStepWrite
                {
                    Key = "monitor",
                    AgentKey = "finance",
                    Objective = "Generate a financial summary.",
                    Tools = ["GetFinancialSummary"]
                }
            ]
        });

        await harness.Automation.PauseScheduleAsync(schedule.Id);
        await harness.Worker.RunOneAsync(schedule.Id, CancellationToken.None);
        (await harness.Db.NalvaWorkflowRuns.CountAsync()).Should().Be(0);

        await harness.Automation.ResumeScheduleAsync(schedule.Id);
        harness.Db.NalvaWorkflowRuns.Add(new NalvaWorkflowRun
        {
            DefinitionId = schedule.Id,
            Status = NalvaStatuses.Running,
            TriggerSource = "Schedule",
            CorrelationId = "busy",
            StepsSnapshotJson = "[]"
        });
        await harness.Db.SaveChangesAsync();

        // With MaxConcurrentRuns=1 and a running run, the worker must not start another.
        var definition = await harness.Db.NalvaWorkflowDefinitions.SingleAsync(x => x.Id == schedule.Id);
        definition.LastScheduleWindow = null;
        definition.NextRunAt = DateTime.UtcNow.AddMinutes(-1);
        await harness.Db.SaveChangesAsync();
        await harness.Worker.RunOneAsync(schedule.Id, CancellationToken.None);
        (await harness.Db.NalvaWorkflowRuns.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Automation_IsTenantIsolated()
    {
        var database = Guid.NewGuid().ToString();
        var left = Harness.Open(database);
        var right = Harness.Open(database);
        await left.Service.ActivateAsync("sales");
        await left.Automation.SaveScheduleAsync(new NalvaScheduleWriteRequest
        {
            Name = "Left only",
            RecurrenceKind = NalvaRecurrenceKinds.Daily,
            Activate = true,
            Steps = [new NalvaWorkflowStepWrite { Key = "a", AgentKey = "sales", Objective = "Follow up", Tools = [] }]
        });

        (await right.Automation.GetSchedulesAsync()).Should().BeEmpty();
        (await right.Automation.GetDashboardAsync()).ScheduledAutomations.Should().Be(0);
    }

    [Fact]
    public async Task ExpiredApprovals_AreNotExecutable()
    {
        var harness = Harness.Open();
        var expired = await harness.Automation.SaveApprovalPolicyAsync(new NalvaApprovalPolicyWriteRequest
        {
            ApprovalExpiryDays = 1,
            AutoExpireApprovals = true,
            NotifyPendingApprovals = true
        });
        expired.ApprovalExpiryDays.Should().Be(1);

        harness.Db.NalvaApprovals.Add(new NalvaApproval
        {
            ExecutionId = Guid.NewGuid(),
            AgentKey = "sales",
            Action = "SendExternalEmail",
            ToolName = "SendExternalEmail",
            Status = NalvaStatuses.Pending,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await harness.Db.SaveChangesAsync();

        var count = await harness.Automation.ExpireApprovalsAsync();
        count.Should().Be(1);
        (await harness.Db.NalvaApprovals.SingleAsync()).Status.Should().Be(NalvaStatuses.Expired);
    }

    [Fact]
    public async Task Dashboard_SurfacesFailedRunsAndAgentActivity()
    {
        var harness = Harness.Open();
        await harness.Service.ActivateAsync("sales");
        var schedule = await harness.Automation.SaveScheduleAsync(new NalvaScheduleWriteRequest
        {
            Name = "Pipeline scan",
            RecurrenceKind = NalvaRecurrenceKinds.Daily,
            Activate = true,
            Steps = [new NalvaWorkflowStepWrite { Key = "a", AgentKey = "sales", Objective = "Scan pipeline", Tools = [] }]
        });
        harness.Db.NalvaWorkflowRuns.Add(new NalvaWorkflowRun
        {
            DefinitionId = schedule.Id,
            Status = NalvaStatuses.Failed,
            Error = "tool timeout",
            TriggerSource = "Schedule",
            StepsSnapshotJson = "[]",
            StartedAt = DateTime.UtcNow
        });
        await harness.Db.SaveChangesAsync();

        var dashboard = await harness.Automation.GetDashboardAsync();
        dashboard.FailedToday.Should().BeGreaterThan(0);
        dashboard.ScheduledAutomations.Should().BeGreaterThan(0);
        (await harness.Automation.GetFailedAsync()).Should().Contain(x => x.Error == "tool timeout");
    }

    private sealed class Harness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaAutomationService Automation { get; init; }
        public required NalvaScheduleWorker Worker { get; init; }

        public static Harness Open(string? database = null)
        {
            var (db, _, provider) = TenantDatabase.Open(name: database);
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(provider.TenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
            var gateway = new NalvaToolGateway(db, provider, new CrmOperationsService(db, user.Object));
            var engine = new NalvaExecutionEngine(db, provider, user.Object, gateway, new SilentModel(), NullLogger<NalvaExecutionEngine>.Instance);
            var workflows = new NalvaWorkflowEngine(db, engine);
            var service = new NalvaWorkforceService(db, user.Object, engine, workflows);
            var automation = new NalvaAutomationService(db, user.Object, service);
            var worker = new NalvaScheduleWorker(db, workflows, NullLogger<NalvaScheduleWorker>.Instance);
            return new Harness { Db = db, Service = service, Automation = automation, Worker = worker };
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

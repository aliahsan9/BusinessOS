using BusinessOS.Application.Features.Nalva;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Nalva;

/// <summary>
/// Safe recovery after application restarts: stalled Running claims become DeadLetter (uncertain)
/// or requeue when no execution was started; expired pending approvals are marked Expired.
/// </summary>
public sealed class NalvaReliabilityService : INalvaReliabilityService
{
    private readonly BusinessOSDbContext _db;
    private readonly ILogger<NalvaReliabilityService> _logger;

    public NalvaReliabilityService(BusinessOSDbContext db, ILogger<NalvaReliabilityService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<NalvaRecoveryResult> RecoverStalledWorkAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var leaseCutoff = now.AddSeconds(-NalvaLimits.ClaimLeaseSeconds);
        var tasksRecovered = 0;
        var executionsClosed = 0;
        var eventsRecovered = 0;
        var approvalsExpired = 0;

        // Cross-tenant recovery — processors already IgnoreQueryFilters for claims.
        var stalledTasks = await _db.NalvaTasks.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                        && x.Status == NalvaStatuses.Running
                        && (x.UpdatedAt ?? x.CreatedAt) < leaseCutoff)
            .OrderBy(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(40)
            .ToListAsync(cancellationToken);

        foreach (var task in stalledTasks)
        {
            var hasRunningExecution = await _db.NalvaExecutions.IgnoreQueryFilters()
                .AnyAsync(x => !x.IsDeleted && x.TaskId == task.Id && x.Status == NalvaStatuses.Running, cancellationToken);

            task.ClaimToken = null;
            task.UpdatedAt = now;
            task.Attempt += 1;
            task.LastError = NalvaGuard.Clip(
                hasRunningExecution
                    ? "Recovered after stalled claim with an open execution. Verify side effects before retry."
                    : "Recovered after stalled claim (no open execution).",
                500);
            task.ErrorClass = hasRunningExecution ? NalvaErrorClass.Uncertain : NalvaErrorClass.Transient;

            if (hasRunningExecution || task.Attempt >= task.MaxAttempts || task.ErrorClass == NalvaErrorClass.Uncertain)
            {
                task.Status = NalvaStatuses.DeadLetter;
            }
            else
            {
                task.Status = NalvaStatuses.Assigned;
                task.NextAttemptAt = now.AddSeconds(Math.Pow(2, Math.Min(task.Attempt, 6)));
                task.RunAfter = now;
            }

            tasksRecovered++;
        }

        var stalledExecutions = await _db.NalvaExecutions.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                        && x.Status == NalvaStatuses.Running
                        && x.StartedAt < leaseCutoff)
            .OrderBy(x => x.StartedAt)
            .Take(40)
            .ToListAsync(cancellationToken);

        foreach (var execution in stalledExecutions)
        {
            execution.Status = NalvaStatuses.Failed;
            execution.Error = NalvaGuard.Clip("Execution stalled past the claim lease and was closed safely after restart.", 500);
            execution.CompletedAt = now;
            execution.UpdatedAt = now;
            if (execution.DurationMs <= 0)
                execution.DurationMs = (int)Math.Min(int.MaxValue, (now - execution.StartedAt).TotalMilliseconds);
            executionsClosed++;
        }

        var stalledEvents = await _db.NalvaBusinessEvents.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                        && x.Status == "Running"
                        && (x.UpdatedAt ?? x.OccurredAt) < leaseCutoff)
            .OrderBy(x => x.UpdatedAt ?? x.OccurredAt)
            .Take(40)
            .ToListAsync(cancellationToken);

        foreach (var row in stalledEvents)
        {
            row.Attempt += 1;
            row.LastError = NalvaGuard.Clip("Recovered after stalled event dispatch.", 500);
            row.UpdatedAt = now;
            if (row.Attempt >= NalvaLimits.MaxRetries)
            {
                row.Status = "DeadLetter";
            }
            else
            {
                row.Status = "Pending";
                row.NextAttemptAt = now.AddSeconds(Math.Pow(2, Math.Min(row.Attempt, 6)));
            }

            eventsRecovered++;
        }

        var expiredApprovals = await _db.NalvaApprovals.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted
                        && (x.Status == NalvaStatuses.Pending || x.Status == NalvaStatuses.ChangesRequested)
                        && x.ExpiresAt != null
                        && x.ExpiresAt < now)
            .OrderBy(x => x.ExpiresAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var approval in expiredApprovals)
        {
            // Only expire when the company still allows auto-expire (default true when settings missing).
            var settings = await _db.NalvaCompanySettings.IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == approval.TenantId && !x.IsDeleted, cancellationToken);
            if (settings is not null && !settings.AutoExpireApprovals)
                continue;

            approval.Status = NalvaStatuses.Expired;
            approval.ExecutionResult = "Approval expired before a person decided.";
            approval.DecidedAt = now;
            approval.UpdatedAt = now;
            approvalsExpired++;
        }

        if (tasksRecovered + executionsClosed + eventsRecovered + approvalsExpired > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Nalva recovery: tasks={Tasks}, executions={Executions}, events={Events}, approvals={Approvals}",
                tasksRecovered, executionsClosed, eventsRecovered, approvalsExpired);
        }

        return new NalvaRecoveryResult(tasksRecovered, executionsClosed, eventsRecovered, approvalsExpired);
    }
}

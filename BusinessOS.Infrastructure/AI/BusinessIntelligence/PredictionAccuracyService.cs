using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.BusinessIntelligence;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.BusinessIntelligence;

public sealed class PredictionAccuracyService : IPredictionAccuracyService
{
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _user;

    public PredictionAccuracyService(BusinessOSDbContext db, ITenantProvider tenant, ICurrentUserService user)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
    }

    public async Task RecordForecastAsync(PredictionRecordRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant())
            return;

        var exists = await _db.PredictionAccuracyLogs.AnyAsync(x =>
            x.MetricKey == request.MetricKey
            && x.TargetPeriodStart == request.TargetPeriodStart
            && x.Status == "Pending", cancellationToken);
        if (exists)
            return;

        _db.PredictionAccuracyLogs.Add(new PredictionAccuracyLog
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            MetricKey = request.MetricKey,
            Method = request.Method,
            PredictedValue = request.PredictedValue,
            Confidence = request.Confidence,
            TargetPeriodStart = request.TargetPeriodStart,
            TargetPeriodEnd = request.TargetPeriodEnd,
            Status = "Pending",
            AssumptionsJson = request.AssumptionsJson
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EvaluateDuePredictionsAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenant.HasTenant())
            return;

        var due = await _db.PredictionAccuracyLogs
            .Where(x => x.Status == "Pending" && x.TargetPeriodEnd < DateTime.UtcNow)
            .OrderBy(x => x.TargetPeriodEnd)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var row in due)
        {
            var actual = await ResolveActualAsync(row.MetricKey, row.TargetPeriodStart, row.TargetPeriodEnd, cancellationToken);
            if (actual is null)
            {
                row.Status = "Skipped";
                row.EvaluatedAt = DateTime.UtcNow;
                continue;
            }

            row.ActualValue = actual;
            if (actual == 0m && row.PredictedValue == 0m)
                row.AbsolutePercentError = 0m;
            else if (actual == 0m)
                row.AbsolutePercentError = null;
            else
                row.AbsolutePercentError = decimal.Round(Math.Abs(actual.Value - row.PredictedValue) / Math.Abs(actual.Value) * 100m, 2);

            row.Status = "Evaluated";
            row.EvaluatedAt = DateTime.UtcNow;
        }

        if (due.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PredictionAccuracySummaryDto> GetAccuracySummaryAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.AnalyticsView, PermissionCodes.NalvaView, PermissionCodes.ReportView);
        await EvaluateDuePredictionsAsync(cancellationToken);

        var rows = await _db.PredictionAccuracyLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var evaluated = rows.Where(x => x.Status == "Evaluated" && x.AbsolutePercentError is not null).ToList();
        decimal? avg = evaluated.Count == 0
            ? null
            : decimal.Round(evaluated.Average(x => x.AbsolutePercentError!.Value), 2);

        return new PredictionAccuracySummaryDto(
            rows.Count,
            evaluated.Count,
            rows.Count(x => x.Status == "Pending"),
            avg,
            rows.Take(20).Select(x => new PredictionAccuracyRowDto(
                x.Id,
                x.MetricKey,
                x.Method,
                x.PredictedValue,
                x.ActualValue,
                x.AbsolutePercentError,
                x.Confidence,
                x.TargetPeriodStart,
                x.TargetPeriodEnd,
                x.EvaluatedAt,
                x.Status)).ToList(),
            "REAL");
    }

    private async Task<decimal?> ResolveActualAsync(string metricKey, DateTime start, DateTime end, CancellationToken ct)
    {
        if (metricKey is "sales-revenue-monthly" or "revenue-monthly")
        {
            return await _db.Orders.AsNoTracking()
                .Where(x => x.Status == OrderStatusNames.Completed && x.OrderDate >= start && x.OrderDate <= end)
                .SumAsync(x => (decimal?)x.GrandTotal, ct) ?? 0m;
        }

        return null;
    }

    private void RequireAny(params string[] permissions)
    {
        if (permissions.Any(_user.HasPermission))
            return;
        throw new ForbiddenException($"Missing permission {string.Join(" or ", permissions)}.");
    }
}

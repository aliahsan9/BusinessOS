using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Autopilot;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.Autopilot;

public sealed class BusinessGoalService : IBusinessGoalService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ITenantProvider _tenant;

    public BusinessGoalService(BusinessOSDbContext db, ICurrentUserService user, ITenantProvider tenant)
    {
        _db = db;
        _user = user;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<BusinessGoalDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var rows = await _db.BusinessGoals.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<BusinessGoalDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaView);
        var row = await _db.BusinessGoals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Goal was not found.");
        return Map(row);
    }

    public async Task<BusinessGoalDto> CreateAsync(BusinessGoalWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        Validate(request);
        var row = new BusinessGoal
        {
            TenantId = _tenant.TenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? "",
            MetricKey = request.MetricKey.Trim().ToLowerInvariant(),
            TargetValue = request.TargetValue,
            BaselineValue = request.BaselineValue,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "currency" : request.Unit.Trim(),
            PeriodStartUtc = request.PeriodStartUtc,
            PeriodEndUtc = request.PeriodEndUtc,
            AssignedAgentKeysJson = JsonSerializer.Serialize(request.AssignedAgentKeys ?? [], JsonOptions),
            Status = "Active"
        };
        _db.BusinessGoals.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<BusinessGoalDto> UpdateAsync(Guid id, BusinessGoalWriteRequest request, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        Validate(request);
        var row = await _db.BusinessGoals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Goal was not found.");
        row.Name = request.Name.Trim();
        row.Description = request.Description?.Trim() ?? "";
        row.MetricKey = request.MetricKey.Trim().ToLowerInvariant();
        row.TargetValue = request.TargetValue;
        row.BaselineValue = request.BaselineValue;
        row.Unit = string.IsNullOrWhiteSpace(request.Unit) ? row.Unit : request.Unit.Trim();
        row.PeriodStartUtc = request.PeriodStartUtc;
        row.PeriodEndUtc = request.PeriodEndUtc;
        if (request.AssignedAgentKeys is not null)
            row.AssignedAgentKeysJson = JsonSerializer.Serialize(request.AssignedAgentKeys, JsonOptions);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task<BusinessGoalDto> RefreshActualAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await _db.BusinessGoals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Goal was not found.");

        var (actual, evidence) = await MeasureAsync(row.MetricKey, row.PeriodStartUtc, row.PeriodEndUtc, cancellationToken);
        row.ActualValue = actual;
        row.EvidenceLinked = evidence;
        row.LastMeasuredAt = DateTime.UtcNow;
        // Predicted is never silently promoted to Actual.
        if (actual is not null && row.TargetValue != 0)
        {
            var progress = actual.Value / row.TargetValue;
            if (progress >= 1m && DateTime.UtcNow >= row.PeriodEndUtc)
                row.Status = "Achieved";
        }

        _db.BusinessGoalProgress.Add(new BusinessGoalProgress
        {
            TenantId = _tenant.TenantId,
            GoalId = row.Id,
            TargetValue = row.TargetValue,
            PredictedValue = row.PredictedValue,
            ActualValue = row.ActualValue,
            Source = "measured",
            Notes = evidence ? "Measured from tenant records." : "Metric source unavailable; actual left unset.",
            MeasuredAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Require(PermissionCodes.NalvaManage);
        var row = await _db.BusinessGoals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Goal was not found.");
        row.IsDeleted = true;
        row.Status = "Cancelled";
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(decimal? Actual, bool Evidence)> MeasureAsync(
        string metricKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        switch (metricKey)
        {
            case "monthly_revenue":
            case "revenue":
            {
                var total = await _db.Orders.AsNoTracking()
                    .Where(x => x.OrderDate >= from && x.OrderDate <= to && !x.IsDeleted)
                    .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
                return (total, true);
            }
            case "overdue_payments":
            case "overdue_ar":
            {
                var count = await _db.Invoices.AsNoTracking()
                    .CountAsync(x => !x.IsDeleted && x.DueDate < DateTime.UtcNow && x.Status != "Paid" && x.Status != "Cancelled", cancellationToken);
                return (count, true);
            }
            case "stockouts":
            case "stockout_count":
            {
                var count = await _db.Products.AsNoTracking()
                    .CountAsync(x => !x.IsDeleted && x.CurrentStock <= 0, cancellationToken);
                return (count, true);
            }
            case "operating_expenses":
            {
                var total = await _db.Expenses.AsNoTracking()
                    .Where(x => x.ExpenseDate >= from && x.ExpenseDate <= to && !x.IsDeleted)
                    .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
                return (total, true);
            }
            default:
                return (null, false);
        }
    }

    private static void Validate(BusinessGoalWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Goal name is required.");
        if (string.IsNullOrWhiteSpace(request.MetricKey))
            throw new BadRequestException("Metric key is required.");
        if (request.PeriodEndUtc <= request.PeriodStartUtc)
            throw new BadRequestException("Period end must be after period start.");
    }

    private void Require(string permission)
    {
        if (!_user.HasPermission(permission))
            throw new ForbiddenException("Missing permission.");
    }

    private static IReadOnlyList<string> ParseStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static BusinessGoalDto Map(BusinessGoal row)
    {
        decimal? progress = null;
        if (row.ActualValue is decimal actual && row.TargetValue != 0)
            progress = Math.Round(actual / row.TargetValue * 100m, 2);
        return new BusinessGoalDto
        {
            Id = row.Id,
            Name = row.Name,
            Description = row.Description,
            MetricKey = row.MetricKey,
            TargetValue = row.TargetValue,
            BaselineValue = row.BaselineValue,
            PredictedValue = row.PredictedValue,
            ActualValue = row.ActualValue,
            Unit = row.Unit,
            Status = row.Status,
            PeriodStartUtc = row.PeriodStartUtc,
            PeriodEndUtc = row.PeriodEndUtc,
            AssignedAgentKeys = ParseStrings(row.AssignedAgentKeysJson),
            RecommendedActions = ParseStrings(row.RecommendedActionsJson),
            CompletedActions = ParseStrings(row.CompletedActionsJson),
            OutcomeNotes = row.OutcomeNotes,
            LastMeasuredAt = row.LastMeasuredAt,
            EvidenceLinked = row.EvidenceLinked,
            ProgressPercent = progress
        };
    }
}

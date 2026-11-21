using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.BusinessIntelligence;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.BusinessIntelligence;

public sealed class CustomerIntelligenceService : ICustomerIntelligenceService
{
    private readonly BusinessOSDbContext _db;
    private readonly ICurrentUserService _user;

    public CustomerIntelligenceService(BusinessOSDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    public async Task<CustomerIntelligenceDto> GetIntelligenceAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.CustomerView, PermissionCodes.AnalyticsView, PermissionCodes.NalvaView);
        var retention = await GetRetentionAsync(cancellationToken);
        var segments = await GetBehavioralSegmentsAsync(cancellationToken);
        var churn = await GetChurnRisksAsync(cancellationToken);
        var strategies = await GetRetentionStrategiesAsync(cancellationToken);
        return new CustomerIntelligenceDto(retention, segments, churn, strategies, "REAL");
    }

    public async Task<IReadOnlyList<CustomerSegmentInsightDto>> GetBehavioralSegmentsAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.CustomerView, PermissionCodes.AnalyticsView, PermissionCodes.NalvaView);
        var now = DateTime.UtcNow;
        var stats = await LoadCustomerOrderStatsAsync(cancellationToken);

        var champions = stats.Where(x => x.OrderCount >= 3 && x.LastOrder >= now.AddDays(-60)).ToList();
        var loyal = stats.Where(x => x.OrderCount >= 2 && x.LastOrder >= now.AddDays(-90) && !champions.Contains(x)).ToList();
        var atRisk = stats.Where(x => x.OrderCount >= 1 && x.LastOrder < now.AddDays(-60) && x.LastOrder >= now.AddDays(-180)).ToList();
        var lost = stats.Where(x => x.LastOrder < now.AddDays(-180)).ToList();
        var newBuyers = stats.Where(x => x.OrderCount == 1 && x.LastOrder >= now.AddDays(-60)).ToList();

        return
        [
            new("champions", "Champions", champions.Count, champions.Sum(x => x.Revenue),
                "3+ completed orders with a purchase in the last 60 days."),
            new("loyal", "Loyal", loyal.Count, loyal.Sum(x => x.Revenue),
                "2+ completed orders and active within 90 days (excluding champions)."),
            new("new", "New buyers", newBuyers.Count, newBuyers.Sum(x => x.Revenue),
                "Exactly one completed order in the last 60 days."),
            new("at-risk", "At risk", atRisk.Count, atRisk.Sum(x => x.Revenue),
                "Previously purchased, but quiet for 60–180 days."),
            new("lost", "Potentially lost", lost.Count, lost.Sum(x => x.Revenue),
                "No completed order in over 180 days. Heuristic risk — not a confirmed churn event.")
        ];
    }

    public async Task<IReadOnlyList<ChurnRiskCustomerDto>> GetChurnRisksAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.CustomerView, PermissionCodes.AnalyticsView, PermissionCodes.NalvaView);
        var now = DateTime.UtcNow;
        var stats = await LoadCustomerOrderStatsAsync(cancellationToken);
        var customers = await _db.Customers.AsNoTracking()
            .Select(x => new { x.Id, Name = (x.FirstName + " " + x.LastName).Trim(), x.Email })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return stats
            .Where(x => x.LastOrder < now.AddDays(-60))
            .Select(x =>
            {
                customers.TryGetValue(x.CustomerId, out var c);
                var days = (int)(now.Date - x.LastOrder.Date).TotalDays;
                var risk = days >= 180 ? "high" : days >= 90 ? "medium" : "low";
                return new ChurnRiskCustomerDto(
                    x.CustomerId,
                    c?.Name ?? "Customer",
                    c?.Email ?? "",
                    x.LastOrder,
                    days,
                    x.OrderCount,
                    x.Revenue,
                    risk,
                    days >= 180
                        ? "No completed orders in 180+ days after prior purchase history."
                        : "Engagement declining: last completed order was more than 60 days ago.",
                    IsPrediction: true);
            })
            .OrderByDescending(x => x.RiskLevel == "high" ? 2 : x.RiskLevel == "medium" ? 1 : 0)
            .ThenByDescending(x => x.DaysSinceLastOrder)
            .Take(50)
            .ToList();
    }

    public async Task<CustomerRetentionDto> GetRetentionAsync(CancellationToken cancellationToken = default)
    {
        RequireAny(PermissionCodes.CustomerView, PermissionCodes.AnalyticsView, PermissionCodes.NalvaView);
        var now = DateTime.UtcNow;
        var stats = await LoadCustomerOrderStatsAsync(cancellationToken);
        var active = stats.Count(x => x.LastOrder >= now.AddDays(-90));
        var repeat = stats.Count(x => x.OrderCount >= 2);
        var atRisk = stats.Count(x => x.LastOrder < now.AddDays(-60) && x.LastOrder >= now.AddDays(-180));
        var lost = stats.Count(x => x.LastOrder < now.AddDays(-180));
        var buyers = stats.Count;
        var repeatRate = buyers == 0 ? 0m : decimal.Round((decimal)repeat / buyers, 4);

        // Retention estimate: share of customers with an order in prior 90–180 window who also ordered in last 90 days.
        var priorCohort = stats.Where(x => x.FirstOrder < now.AddDays(-90)).ToList();
        var retained = priorCohort.Count(x => x.LastOrder >= now.AddDays(-90));
        decimal? retention = priorCohort.Count == 0
            ? null
            : decimal.Round((decimal)retained / priorCohort.Count * 100m, 2);

        var confidence = priorCohort.Count >= 20 ? "medium" : priorCohort.Count >= 5 ? "low" : "unavailable";
        var assumptions = new List<string>
        {
            "Active = completed order in last 90 days.",
            "Repeat = 2+ lifetime completed orders.",
            "Retention estimate compares customers with first order before 90 days ago who also ordered in the last 90 days."
        };
        var limitations = new List<string>();
        if (priorCohort.Count < 5)
            limitations.Add("Insufficient cohort size for a stable retention estimate.");
        limitations.Add("Churn and retention figures are heuristics from order history — not survey-confirmed outcomes.");

        return new CustomerRetentionDto(
            active,
            repeat,
            repeatRate,
            atRisk,
            lost,
            retention,
            confidence,
            assumptions,
            limitations,
            IsPrediction: retention is not null);
    }

    public async Task<IReadOnlyList<BiRecommendationDto>> GetRetentionStrategiesAsync(CancellationToken cancellationToken = default)
    {
        var churn = await GetChurnRisksAsync(cancellationToken);
        var retention = await GetRetentionAsync(cancellationToken);
        var list = new List<BiRecommendationDto>();

        var high = churn.Count(x => x.RiskLevel == "high");
        if (high > 0)
        {
            list.Add(new BiRecommendationDto(
                "reengage-high-churn",
                "customer",
                "Re-engage high churn-risk customers",
                $"{high} customer(s) have no completed orders in 180+ days.",
                [$"High risk: {high}", $"At risk (60–180d): {retention.AtRiskCustomers}"],
                "support",
                "Prepare personalized re-engagement outreach for high churn-risk customers.",
                RequiresHumanApproval: true,
                IsPrediction: true,
                Confidence: "medium"));
        }

        if (retention.RepeatRate < 0.25m && retention.ActiveCustomers >= 5)
        {
            list.Add(new BiRecommendationDto(
                "raise-repeat-rate",
                "customer",
                "Improve repeat purchase rate",
                "Fewer than 25% of buyers have placed 2+ completed orders.",
                [$"Repeat rate: {retention.RepeatRate}", $"Active: {retention.ActiveCustomers}"],
                "marketing",
                "Design a loyalty or reorder campaign for one-time buyers.",
                RequiresHumanApproval: true,
                IsPrediction: false,
                Confidence: "medium"));
        }

        if (churn.Any(x => x.LifetimeRevenue >= 500m && x.RiskLevel is "high" or "medium"))
        {
            list.Add(new BiRecommendationDto(
                "protect-high-value",
                "customer",
                "Protect high-value quiet customers",
                "High-lifetime-value customers show declining engagement.",
                ["Filter: lifetime revenue >= 500 with 60+ days since last order"],
                "sales",
                "Assign follow-up for high-value customers with declining engagement.",
                RequiresHumanApproval: true,
                IsPrediction: true,
                Confidence: "medium"));
        }

        return list;
    }

    private async Task<List<CustomerOrderStat>> LoadCustomerOrderStatsAsync(CancellationToken ct)
    {
        var rows = await _db.Orders.AsNoTracking()
            .Where(x => x.Status == OrderStatusNames.Completed)
            .GroupBy(x => x.CustomerId)
            .Select(g => new CustomerOrderStat(
                g.Key,
                g.Count(),
                g.Sum(x => x.GrandTotal),
                g.Min(x => x.OrderDate),
                g.Max(x => x.OrderDate)))
            .ToListAsync(ct);
        return rows;
    }

    private void RequireAny(params string[] permissions)
    {
        if (permissions.Any(_user.HasPermission))
            return;
        throw new ForbiddenException($"Missing permission {string.Join(" or ", permissions)}.");
    }

    private sealed record CustomerOrderStat(
        Guid CustomerId,
        int OrderCount,
        decimal Revenue,
        DateTime FirstOrder,
        DateTime LastOrder);
}

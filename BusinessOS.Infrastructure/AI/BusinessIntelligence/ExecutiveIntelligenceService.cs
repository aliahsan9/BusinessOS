using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.BusinessIntelligence;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.ProcurementAgent;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.BusinessIntelligence;

public sealed class ExecutiveIntelligenceService : IExecutiveIntelligenceService
{
    private readonly IBusinessIntelligenceService _bi;
    private readonly ISalesIntelligenceService _sales;
    private readonly ICustomerIntelligenceService _customers;
    private readonly IInventoryProcurementAgentService _procurement;
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserService _user;

    public ExecutiveIntelligenceService(
        IBusinessIntelligenceService bi,
        ISalesIntelligenceService sales,
        ICustomerIntelligenceService customers,
        IInventoryProcurementAgentService procurement,
        BusinessOSDbContext db,
        ITenantProvider tenant,
        ICurrentUserService user)
    {
        _bi = bi;
        _sales = sales;
        _customers = customers;
        _procurement = procurement;
        _db = db;
        _tenant = tenant;
        _user = user;
    }

    public Task<DecisionBriefDto> AnswerAsync(string question, CancellationToken cancellationToken = default)
    {
        var q = (question ?? "").Trim().ToLowerInvariant();
        if (Contains(q, "how is my business", "business performing", "business health", "how are we doing"))
            return BusinessPerformanceAsync(cancellationToken);
        if (Contains(q, "which products should i promote", "products to promote", "promote"))
            return ProductsToPromoteAsync(cancellationToken);
        if (Contains(q, "immediate attention", "problems", "what needs attention", "urgent"))
            return ImmediateProblemsAsync(cancellationToken);
        if (Contains(q, "customers might stop", "churn", "stop buying", "lost customers"))
            return CustomersAtRiskAsync(cancellationToken);
        if (Contains(q, "how much inventory", "inventory should i purchase", "reorder", "stock to buy"))
            return InventoryPurchaseAdviceAsync(cancellationToken);
        if (Contains(q, "expected revenue", "revenue next month", "forecast revenue", "predict sales"))
            return ExpectedRevenueNextMonthAsync(cancellationToken);
        if (Contains(q, "losing money", "where am i losing", "loss", "margin"))
            return WhereLosingMoneyAsync(cancellationToken);
        return BusinessPerformanceAsync(cancellationToken);
    }

    public async Task<DecisionBriefDto> BusinessPerformanceAsync(CancellationToken cancellationToken = default)
    {
        var health = await _bi.GetHealthOverviewAsync(period: "month", cancellationToken: cancellationToken);
        var comparison = await _bi.ComparePeriodsAsync(period: "month", cancellationToken: cancellationToken);
        var metrics = new List<string>
        {
            $"Health label: {health.OverallHealthLabel} (heuristic)",
            $"Sales revenue (completed orders): {health.SalesRevenue.Value} {health.Currency}",
            $"Collected cash: {health.CollectedCash.Value} {health.Currency}",
            $"Expenses (approved/paid): {health.Expenses.Value} {health.Currency}",
            $"Revenue vs prior period: {comparison.RevenueChangePercent}%",
            $"Low/out-of-stock SKUs: {health.LowStockCount}",
            $"Churn-risk customers: {health.ChurnRiskCount}",
            $"Overdue invoices: {health.OverdueInvoiceCount}"
        };
        if (health.GrossProfit.Available)
            metrics.Add($"Gross profit: {health.GrossProfit.Value} {health.Currency}");
        else
            metrics.Add("Gross profit unavailable (missing COGS).");

        var answer =
            $"Based on recorded data for {health.Period}, overall health is labeled '{health.OverallHealthLabel}'. " +
            $"Completed-order revenue is {health.SalesRevenue.Value} {health.Currency} " +
            $"({comparison.RevenueChangePercent}% vs the prior equal-length period). " +
            "These are actual recorded figures, not forecasts.";

        return Brief(
            "business-performance",
            "How is my business performing?",
            answer,
            metrics,
            (await _bi.GetRecommendationsAsync(cancellationToken)).Take(5).ToList(),
            health.Formulas.ToList(),
            health.Limitations.ToList(),
            "high",
            false);
    }

    public async Task<DecisionBriefDto> ProductsToPromoteAsync(CancellationToken cancellationToken = default)
    {
        var rising = (await _sales.GetProductPerformanceAsync(cancellationToken))
            .Where(x => x.Trend == "rising")
            .Take(5)
            .ToList();
        var metrics = rising.Select(x =>
            $"{x.ProductName} ({x.Sku}): revenue {x.Revenue}, change {x.ChangePercent}%").ToList();
        var actions = rising.Select(x => new BiRecommendationDto(
            $"promote-{x.ProductId:N}",
            "sales",
            $"Promote {x.ProductName}",
            "Rising completed-order revenue vs prior 30 days.",
            [$"Revenue: {x.Revenue}", $"Change: {x.ChangePercent}%"],
            "marketing",
            $"Promote {x.ProductName} based on rising sales.",
            true,
            false,
            x.Confidence)).ToList();

        var answer = rising.Count == 0
            ? "No products currently show a rising 30-day revenue trend from completed orders. Promotion advice is withheld rather than invented."
            : $"Promote these products with rising completed-order revenue: {string.Join(", ", rising.Select(x => x.ProductName))}.";

        return Brief(
            "products-to-promote",
            "Which products should I promote?",
            answer,
            metrics,
            actions,
            ["Trend uses completed order lines only over last 30 days vs prior 30 days."],
            rising.Count == 0 ? ["Insufficient rising-product signal."] : [],
            rising.Count == 0 ? "unavailable" : "medium",
            false);
    }

    public async Task<DecisionBriefDto> ImmediateProblemsAsync(CancellationToken cancellationToken = default)
    {
        var risks = await _bi.GetRisksAsync(cancellationToken);
        var top = risks.Take(8).ToList();
        var metrics = top.Select(x => $"[{x.Severity}/{x.Category}] {x.Title}: {x.Explanation}").ToList();
        var actions = top.Select(x => new BiRecommendationDto(
            $"risk-{Guid.NewGuid():N}",
            x.Category,
            x.Title,
            x.Explanation,
            x.SupportingMetrics,
            x.SuggestedAgentKey,
            $"Address: {x.Title}",
            x.Category is "inventory" or "finance" or "customer",
            x.IsPrediction,
            x.Severity == "high" ? "medium" : "low")).ToList();

        var answer = top.Count == 0
            ? "No urgent inventory, finance, sales, or customer risks were detected from recorded data right now."
            : $"Immediate attention items ({top.Count}): " + string.Join("; ", top.Take(3).Select(x => x.Title)) + ".";

        return Brief(
            "immediate-problems",
            "What problems need immediate attention?",
            answer,
            metrics,
            actions,
            ["Risks are derived from recorded stock, orders, invoices, and cash-flow estimates."],
            ["Predicted stockout dates and cash shortages are estimates."],
            top.Any(x => x.Severity == "high") ? "medium" : "low",
            top.Any(x => x.IsPrediction));
    }

    public async Task<DecisionBriefDto> CustomersAtRiskAsync(CancellationToken cancellationToken = default)
    {
        var churn = await _customers.GetChurnRisksAsync(cancellationToken);
        var strategies = await _customers.GetRetentionStrategiesAsync(cancellationToken);
        var metrics = churn.Take(10)
            .Select(x => $"{x.CustomerName}: {x.RiskLevel} risk, {x.DaysSinceLastOrder}d since last order, lifetime {x.LifetimeRevenue}")
            .ToList();

        var answer = churn.Count == 0
            ? "No customers currently match the declining-engagement heuristic (60+ days since last completed order)."
            : $"{churn.Count} customer(s) may stop buying based on order silence. This is a risk estimate, not a confirmed churn event. Top risks: "
              + string.Join(", ", churn.Take(5).Select(x => x.CustomerName)) + ".";

        return Brief(
            "customers-at-risk",
            "Which customers might stop buying?",
            answer,
            metrics,
            strategies.ToList(),
            ["Churn risk = previously purchased customers with no completed order for 60+ days."],
            ["Not survey-confirmed. External engagement channels are not inferred."],
            churn.Count == 0 ? "unavailable" : "medium",
            true);
    }

    public async Task<DecisionBriefDto> InventoryPurchaseAdviceAsync(CancellationToken cancellationToken = default)
    {
        var recs = await _procurement.GetRecommendationsAsync(cancellationToken);
        var demand = await _procurement.AnalyzeDemandAsync(cancellationToken: cancellationToken);
        var metrics = recs.Take(10)
            .Select(x => $"{x.ProductName}: suggest {x.SuggestedQuantity} (stock {x.CurrentStock}, inbound {x.IncomingQuantity}, confidence {x.Confidence})")
            .ToList();
        metrics.AddRange(demand.Where(x => x.ForecastAvailable).Take(5)
            .Select(x => $"Demand {x.ProductName}: ~{x.AverageDailyDemand}/day, cover {x.DaysOfCover}d ({x.Confidence})"));

        var actions = recs.Take(5).Select(x => new BiRecommendationDto(
            $"buy-{x.ProductId:N}",
            "inventory",
            $"Purchase {x.SuggestedQuantity} of {x.ProductName}",
            x.Reason,
            [$"Qty: {x.SuggestedQuantity}", $"Supplier: {x.PreferredSupplierName ?? "n/a"}"],
            "procurement",
            $"Prepare purchase order draft for {x.ProductName} qty {x.SuggestedQuantity}.",
            true,
            true,
            x.Confidence)).ToList();

        var answer = recs.Count == 0
            ? "No reorder recommendations are ready from recorded stock and demand. Forecasts are withheld when history is insufficient."
            : $"Suggested purchases (estimates from stock + demand): "
              + string.Join("; ", recs.Take(5).Select(x => $"{x.ProductName}×{x.SuggestedQuantity}"))
              + ". Creating a purchase order still requires human approval.";

        return Brief(
            "inventory-purchase",
            "How much inventory should I purchase?",
            answer,
            metrics,
            actions,
            ["Reorder quantities come from recorded stock, inbound POs, and recent completed sales rates."],
            ["Demand forecasts assume recent rates continue; supplier lead times use PO→receive history when present."],
            recs.Count == 0 ? "unavailable" : "medium",
            true);
    }

    public async Task<DecisionBriefDto> ExpectedRevenueNextMonthAsync(CancellationToken cancellationToken = default)
    {
        var forecast = await _bi.ForecastRevenueAsync(1, cancellationToken);
        var metrics = new List<string>
        {
            $"Forecast available: {forecast.Available}",
            $"Method: {forecast.Method}",
            $"Confidence: {forecast.Confidence}",
            $"Predicted next period: {(forecast.NextPeriodPredicted?.ToString() ?? "n/a")} {forecast.Currency}",
            $"Backtest MAPE: {(forecast.BacktestMapePercent?.ToString() ?? "n/a")}%"
        };
        metrics.AddRange(forecast.History.TakeLast(6).Select(x => $"Actual {x.PeriodStart:yyyy-MM}: {x.ActualValue}"));

        var answer = !forecast.Available
            ? "Expected revenue next month is unavailable — not enough completed-order monthly history for a statistical forecast. No figure was invented."
            : $"Estimated completed-order revenue for the next month is {forecast.NextPeriodPredicted} {forecast.Currency} "
              + $"(method: {forecast.Method}, confidence: {forecast.Confidence}). This is a prediction, not a confirmed financial fact.";

        return Brief(
            "expected-revenue",
            "What is my expected revenue next month?",
            answer,
            metrics,
            [],
            forecast.Assumptions.ToList(),
            forecast.Limitations.ToList(),
            forecast.Confidence,
            true);
    }

    public async Task<DecisionBriefDto> WhereLosingMoneyAsync(CancellationToken cancellationToken = default)
    {
        var dashboard = await _bi.GetExecutiveDashboardAsync("month", cancellationToken);
        var finance = dashboard.Finance;
        var declining = dashboard.Sales.DecliningProducts.Take(5).ToList();
        var metrics = new List<string>();
        if (finance is not null)
        {
            metrics.Add($"Sales revenue: {finance.SalesRevenue.Value}");
            metrics.Add($"Expenses: {finance.TotalExpenses.Value}");
            metrics.Add($"Gross profit available: {finance.GrossProfit.Available} value={finance.GrossProfit.Value}");
            metrics.Add($"Net profit available: {finance.NetProfit.Available} value={finance.NetProfit.Value}");
            metrics.Add($"Overdue receivables: {finance.OverdueReceivables.Value}");
        }
        metrics.AddRange(declining.Select(x => $"Declining product {x.ProductName}: -{x.DeclinePercent}%"));

        string answer;
        if (finance is null)
            answer = "Finance intelligence is not available with the current permissions.";
        else if (!finance.GrossProfit.Available && !finance.NetProfit.Available)
            answer = "Profitability cannot be calculated because product cost (COGS) is missing. Revenue alone is not treated as profit. Review declining products and expense totals instead.";
        else if (finance.NetProfit.Value is < 0)
            answer = $"Net profit is negative ({finance.NetProfit.Value} {finance.Currency}) based on completed-order gross profit minus recognized expenses. Review expense spikes and declining products.";
        else
            answer = $"No overall net loss detected for the period. Watch declining products and overdue receivables ({finance.OverdueReceivables.Value}) as potential margin drains.";

        var actions = (await _bi.GetRecommendationsAsync(cancellationToken))
            .Where(x => x.Category is "sales" or "finance" or "inventory")
            .Take(5)
            .ToList();

        return Brief(
            "losing-money",
            "Where am I losing money?",
            answer,
            metrics,
            actions,
            finance?.Formulas.ToList() ?? [],
            finance?.Limitations.ToList() ?? ["Finance panel unavailable."],
            finance?.GrossProfit.Available == true ? "medium" : "low",
            false);
    }

    public async Task<RecommendedTaskResultDto> CreateRecommendedTaskAsync(
        CreateRecommendedTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_user.HasPermission(PermissionCodes.NalvaManage) && !_user.HasPermission(PermissionCodes.NalvaView))
            throw new ForbiddenException("Missing permission to create agent tasks.");

        if (string.IsNullOrWhiteSpace(request.Objective))
            throw new BadRequestException("Objective is required.");

        var agentKey = string.IsNullOrWhiteSpace(request.AgentKey) ? "analyst" : request.AgentKey.Trim().ToLowerInvariant();
        var agent = await _db.NalvaCompanyAgents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AgentKey == agentKey, cancellationToken);
        if (agent is null)
            throw new BadRequestException($"Agent '{agentKey}' was not found for this company.");
        if (agent.Status != NalvaStatuses.Active)
            throw new BadRequestException("Activate the agent before assigning work.");

        string? idempotencyKey = string.IsNullOrWhiteSpace(request.RecommendationId)
            ? null
            : $"bi-rec:{request.RecommendationId.Trim()}";
        if (idempotencyKey is not null)
        {
            var existing = await _db.NalvaTasks.AsNoTracking().FirstOrDefaultAsync(
                x => x.IdempotencyKey == idempotencyKey
                     && (x.Status == NalvaStatuses.Completed
                         || x.Status == NalvaStatuses.Queued
                         || x.Status == NalvaStatuses.Running
                         || x.Status == NalvaStatuses.AwaitingApproval
                         || x.Status == NalvaStatuses.Paused),
                cancellationToken);
            if (existing is not null)
            {
                return new RecommendedTaskResultDto(
                    true,
                    existing.Id,
                    existing.Status,
                    "Existing task reused (idempotent).",
                    request.RequiresApproval);
            }
        }

        var task = new NalvaTask
        {
            TenantId = _tenant.TenantId,
            AgentKey = agent.AgentKey,
            Objective = request.Objective.Trim(),
            Status = NalvaStatuses.Queued,
            Priority = request.RequiresApproval ? 5 : 3,
            RunAfter = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey,
            InitiatorUserId = _user.UserId
        };
        _db.NalvaTasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken);

        return new RecommendedTaskResultDto(
            true,
            task.Id,
            task.Status,
            request.RequiresApproval
                ? "Task queued. Sensitive tool calls still require human approval before execution."
                : "Task queued for the selected agent.",
            request.RequiresApproval);
    }

    private static DecisionBriefDto Brief(
        string key,
        string question,
        string answer,
        IReadOnlyList<string> metrics,
        IReadOnlyList<BiRecommendationDto> actions,
        IReadOnlyList<string> assumptions,
        IReadOnlyList<string> limitations,
        string confidence,
        bool predictions) =>
        new(key, question, answer, metrics, actions, assumptions, limitations, confidence, predictions, "REAL");

    private static bool Contains(string text, params string[] needles) =>
        needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));
}

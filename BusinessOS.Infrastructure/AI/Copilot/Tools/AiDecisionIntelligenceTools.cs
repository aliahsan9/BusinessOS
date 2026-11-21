using System.Text;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.BusinessIntelligence;

namespace BusinessOS.Infrastructure.AI.Copilot.Tools;

public sealed class GetExecutiveDecisionBriefTool : AiToolBase
{
    private readonly IExecutiveIntelligenceService _executive;

    public GetExecutiveDecisionBriefTool(IExecutiveIntelligenceService executive) => _executive = executive;

    public override AiToolName ToolName => AiToolName.GetExecutiveDecisionBrief;
    public override string Description =>
        "Data-backed decision brief for owner questions (performance, promotions, risks, churn, inventory buys, revenue forecast, losses). Distinguishes actuals from predictions.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message,
            "how is my business", "which products should i promote", "immediate attention",
            "customers might stop", "how much inventory", "expected revenue", "losing money",
            "decision brief", "what should i do");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var brief = await _executive.AnswerAsync(context.Message ?? "How is my business performing?", cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine(brief.Answer);
        sb.AppendLine();
        sb.AppendLine($"Confidence: {brief.Confidence}. Contains predictions: {brief.ContainsPredictions}.");
        if (brief.SupportingMetrics.Count > 0)
        {
            sb.AppendLine("Supporting metrics:");
            foreach (var m in brief.SupportingMetrics.Take(12))
                sb.AppendLine($"- {m}");
        }
        if (brief.RecommendedActions.Count > 0)
        {
            sb.AppendLine("Recommended actions:");
            foreach (var a in brief.RecommendedActions.Take(5))
                sb.AppendLine($"- {a.Title}: {a.Explanation}{(a.RequiresHumanApproval ? " (requires human approval)" : "")}");
        }
        if (brief.Assumptions.Count > 0)
        {
            sb.AppendLine("Assumptions:");
            foreach (var a in brief.Assumptions.Take(5))
                sb.AppendLine($"- {a}");
        }
        if (brief.Limitations.Count > 0)
        {
            sb.AppendLine("Limitations:");
            foreach (var l in brief.Limitations.Take(5))
                sb.AppendLine($"- {l}");
        }
        if (brief.ContainsPredictions)
            sb.AppendLine("Note: Predictions above are estimates, not confirmed financial facts.");

        return new AiToolResult { ToolName = ToolName.ToString(), Summary = sb.ToString().Trim() };
    }
}

public sealed class ForecastSalesRevenueTool : AiToolBase
{
    private readonly ISalesIntelligenceService _sales;

    public ForecastSalesRevenueTool(ISalesIntelligenceService sales) => _sales = sales;

    public override AiToolName ToolName => AiToolName.ForecastSalesRevenue;
    public override string Description =>
        "Statistical next-period sales revenue forecast from completed-order history. Labeled prediction with confidence — never invents figures when history is insufficient.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "forecast revenue", "expected revenue", "predict sales", "revenue next month", "sales forecast");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var forecast = await _sales.ForecastSalesAsync(1, cancellationToken);
        var sb = new StringBuilder();
        if (!forecast.Available)
        {
            sb.AppendLine("Sales revenue forecast is unavailable due to insufficient completed-order history. No figure was invented.");
        }
        else
        {
            sb.AppendLine($"Estimated next-period completed-order revenue: {forecast.NextPeriodPredicted} {forecast.Currency}.");
            sb.AppendLine($"Method: {forecast.Method}. Confidence: {forecast.Confidence}. This is a prediction, not a confirmed financial fact.");
            if (forecast.BacktestMapePercent is not null)
                sb.AppendLine($"Recent one-step backtest MAPE: {forecast.BacktestMapePercent}%.");
        }
        foreach (var a in forecast.Assumptions.Take(4))
            sb.AppendLine($"Assumption: {a}");
        foreach (var l in forecast.Limitations.Take(4))
            sb.AppendLine($"Limitation: {l}");
        return new AiToolResult { ToolName = ToolName.ToString(), Summary = sb.ToString().Trim() };
    }
}

public sealed class GetBusinessRisksTool : AiToolBase
{
    private readonly IBusinessIntelligenceService _bi;

    public GetBusinessRisksTool(IBusinessIntelligenceService bi) => _bi = bi;

    public override AiToolName ToolName => AiToolName.GetBusinessRisks;
    public override string Description =>
        "Cross-domain business risks from recorded inventory, finance, sales, and customer signals.";

    public override bool CanHandle(AiCopilotIntent intent, string message, AiPageContextDto page, AiMemoryStateDto memory) =>
        ContainsAny(message, "business risks", "what problems", "immediate attention", "what needs attention");

    public override async Task<AiToolResult> ExecuteAsync(AiCopilotExecutionContext context, CancellationToken cancellationToken = default)
    {
        var risks = await _bi.GetRisksAsync(cancellationToken);
        var sb = new StringBuilder();
        if (risks.Count == 0)
            sb.AppendLine("No material risks detected from recorded inventory, finance, sales, or customer data.");
        else
        {
            sb.AppendLine($"{risks.Count} risk signal(s) from recorded data:");
            foreach (var r in risks.Take(10))
                sb.AppendLine($"- [{r.Severity}/{r.Category}] {r.Title}: {r.Explanation}{(r.IsPrediction ? " (includes estimate)" : "")}");
        }
        return new AiToolResult { ToolName = ToolName.ToString(), Summary = sb.ToString().Trim() };
    }
}

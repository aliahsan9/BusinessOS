using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.FinanceAgent;

namespace BusinessOS.API.Endpoints;

public static class FinanceAgentEndpoints
{
    public static void MapFinanceAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/nalva/finance")
            .WithTags("Finance Agent")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/overview", async (DateTime? startDate, DateTime? endDate, string? period, IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetOverviewAsync(startDate, endDate, period, ct)));

        group.MapGet("/intelligence", async (DateTime? startDate, DateTime? endDate, string? period, IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetIntelligenceAsync(startDate, endDate, period, ct)));

        group.MapGet("/expenses/analyze", async (DateTime? startDate, DateTime? endDate, string? period, IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.AnalyzeExpensesAsync(startDate, endDate, period, ct)));

        group.MapGet("/expenses/category-suggestions", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.SuggestExpenseCategoriesAsync(ct)));

        group.MapGet("/receivables", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetReceivablesAsync(ct)));

        group.MapGet("/payables", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetPayablesAsync(ct)));

        group.MapGet("/cash-flow", async (DateTime? startDate, DateTime? endDate, int? projectionDays, IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetCashFlowAsync(startDate, endDate, projectionDays ?? 30, ct)));

        group.MapGet("/reconciliation", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.ReconcileAsync(ct)));

        group.MapPost("/bank-statements/import", async (BankStatementImportRequest request, IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.ImportBankStatementCsvAsync(request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/reports", async (FinanceReportRequest request, IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GenerateReportAsync(request, ct)));

        group.MapGet("/budgets", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetBudgetsAsync(ct)));

        group.MapPost("/budgets", async (FinanceBudgetWriteRequest request, IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.UpsertBudgetAsync(request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapGet("/budgets/alerts", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetBudgetAlertsAsync(ct)));

        group.MapGet("/alerts", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetAlertsAsync(ct)));

        group.MapGet("/reminders", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetRemindersAsync(ct)));

        group.MapPost("/reminders/prepare", async (PrepareReminderRequest request, IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.PreparePaymentReminderAsync(request, ct)));

        group.MapPost("/reminders/{reminderId:guid}/submit", async (
            Guid reminderId,
            SubmitFinanceReminderRequest? request,
            IFinanceAgentService service,
            CancellationToken ct) =>
            Results.Ok(await service.SubmitPaymentReminderAsync(
                reminderId,
                request?.Approved == true,
                request?.ApprovalFingerprint,
                request?.IdempotencyKey,
                ct)))
            .RequirePermission(PermissionCodes.NalvaApprove);

        group.MapGet("/activity", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetActivityAsync(ct)));

        group.MapGet("/automation", async (IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.GetAutomationSettingsAsync(ct)));

        group.MapPut("/automation", async (FinanceAutomationSettingsWriteRequest request, IFinanceAgentService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAutomationSettingsAsync(request, ct)))
            .RequirePermission(PermissionCodes.NalvaManage);

        group.MapPost("/monitor/run", async (IFinanceAgentService service, CancellationToken ct) =>
        {
            await service.RunScheduledMonitorAsync(ct);
            return Results.Ok(new { ran = true });
        }).RequirePermission(PermissionCodes.NalvaManage);
    }
}

public sealed class SubmitFinanceReminderRequest
{
    public bool Approved { get; set; }
    public string? ApprovalFingerprint { get; set; }
    public string? IdempotencyKey { get; set; }
}

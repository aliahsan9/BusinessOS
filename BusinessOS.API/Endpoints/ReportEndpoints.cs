using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Reports.DTOs;
using BusinessOS.Application.Features.Reports.Services;

namespace BusinessOS.API.Endpoints;

/// <summary>
/// PDF report generation and history endpoints.
/// </summary>
public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        group.MapGet("/business-summary", GenerateBusinessSummary)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GenerateBusinessSummaryReport")
            .WithSummary("Generate business summary PDF report")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("/revenue", GenerateRevenue)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GenerateRevenueReport")
            .WithSummary("Generate revenue PDF report")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("/expenses", GenerateExpenses)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GenerateExpenseReport")
            .WithSummary("Generate expense PDF report")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("/profit-loss", GenerateProfitLoss)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GenerateProfitLossReport")
            .WithSummary("Generate profit and loss PDF report")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("/customers", GenerateCustomers)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GenerateCustomerReport")
            .WithSummary("Generate customer PDF report")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("/projects", GenerateProjects)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GenerateProjectReport")
            .WithSummary("Generate project PDF report")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("/tasks", GenerateTasks)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GenerateTaskReport")
            .WithSummary("Generate task productivity PDF report")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("/invoice/{id:guid}", GenerateInvoice)
            .RequirePermission(PermissionCodes.InvoiceView)
            .WithName("GenerateInvoicePdfReport")
            .WithSummary("Generate professional invoice PDF")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapGet("/history", GetHistory)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("GetReportHistory")
            .WithSummary("Get generated report history")
            .Produces<ReportHistoryResponse>(StatusCodes.Status200OK);

        group.MapGet("/history/{id:guid}/download", DownloadHistory)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("DownloadReportHistory")
            .WithSummary("Download a previously generated report")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        group.MapPost("/history/{id:guid}/regenerate", RegenerateHistory)
            .RequirePermission(PermissionCodes.ReportView)
            .WithName("RegenerateReportHistory")
            .WithSummary("Regenerate a report from history")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");
    }

    private static async Task<IResult> GenerateBusinessSummary(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.GenerateBusinessSummaryAsync(
            new ReportQueryParams { StartDate = startDate, EndDate = endDate, Period = period },
            cancellationToken));

    private static async Task<IResult> GenerateRevenue(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.GenerateRevenueReportAsync(
            new ReportQueryParams { StartDate = startDate, EndDate = endDate, Period = period },
            cancellationToken));

    private static async Task<IResult> GenerateExpenses(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.GenerateExpenseReportAsync(
            new ReportQueryParams { StartDate = startDate, EndDate = endDate, Period = period },
            cancellationToken));

    private static async Task<IResult> GenerateProfitLoss(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.GenerateProfitLossReportAsync(
            new ReportQueryParams { StartDate = startDate, EndDate = endDate, Period = period },
            cancellationToken));

    private static async Task<IResult> GenerateCustomers(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        Guid? customerId,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.GenerateCustomerReportAsync(
            new ReportQueryParams
            {
                StartDate = startDate,
                EndDate = endDate,
                Period = period,
                CustomerId = customerId
            },
            cancellationToken));

    private static async Task<IResult> GenerateProjects(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        Guid? projectId,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.GenerateProjectReportAsync(
            new ReportQueryParams
            {
                StartDate = startDate,
                EndDate = endDate,
                Period = period,
                ProjectId = projectId
            },
            cancellationToken));

    private static async Task<IResult> GenerateTasks(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.GenerateTaskReportAsync(
            new ReportQueryParams { StartDate = startDate, EndDate = endDate, Period = period },
            cancellationToken));

    private static async Task<IResult> GenerateInvoice(
        Guid id,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.GenerateInvoicePdfAsync(id, cancellationToken));

    private static async Task<IResult> GetHistory(
        IReportService reportService,
        CancellationToken cancellationToken)
    {
        var result = await reportService.GetHistoryAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> DownloadHistory(
        Guid id,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.DownloadHistoryAsync(id, cancellationToken));

    private static async Task<IResult> RegenerateHistory(
        Guid id,
        IReportService reportService,
        CancellationToken cancellationToken) =>
        await PdfResult(reportService.RegenerateHistoryAsync(id, cancellationToken));

    private static async Task<IResult> PdfResult(Task<ReportGenerationResult> task)
    {
        var result = await task;
        return Results.File(result.Content, result.ContentType, result.FileName);
    }
}

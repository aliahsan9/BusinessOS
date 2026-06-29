using BusinessOS.Application.Features.Reports.DTOs;

namespace BusinessOS.Application.Features.Reports.Services;

public interface IReportService
{
    Task<ReportGenerationResult> GenerateBusinessSummaryAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> GenerateRevenueReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> GenerateExpenseReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> GenerateProfitLossReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> GenerateCustomerReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> GenerateProjectReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> GenerateTaskReportAsync(
        ReportQueryParams query,
        CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> GenerateInvoicePdfAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<ReportHistoryResponse> GetHistoryAsync(
        CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> DownloadHistoryAsync(
        Guid historyId,
        CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> RegenerateHistoryAsync(
        Guid historyId,
        CancellationToken cancellationToken = default);
}

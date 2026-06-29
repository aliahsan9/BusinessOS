using BusinessOS.Application.Features.Pdf.Models;

namespace BusinessOS.Application.Features.Pdf.Services;

public interface IPdfGenerationService
{
    byte[] GenerateBusinessSummary(BusinessSummaryPdfModel model);
    byte[] GenerateRevenueReport(RevenueReportPdfModel model);
    byte[] GenerateExpenseReport(ExpenseReportPdfModel model);
    byte[] GenerateProfitLossReport(ProfitLossPdfModel model);
    byte[] GenerateCustomerReport(CustomerReportPdfModel model);
    byte[] GenerateProjectReport(ProjectReportPdfModel model);
    byte[] GenerateTaskReport(TaskReportPdfModel model);
    byte[] GenerateInvoice(InvoicePdfModel model);
}

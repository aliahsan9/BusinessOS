using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Billing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BusinessOS.Infrastructure.Services;

public static class BillingInvoicePdf
{
    public static byte[] Render(BillingInvoice invoice, bool asReceipt = false)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var amount = CurrencyMath.Round(invoice.TotalAmount, invoice.Currency);
        var title = asReceipt ? "CompanyOS receipt" : "CompanyOS invoice";
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(style => style.FontSize(11));
                page.Header().Text(title).FontSize(20).SemiBold().FontColor(Colors.Blue.Darken3);
                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text($"Invoice {invoice.InvoiceNumber}");
                    if (!string.IsNullOrWhiteSpace(invoice.ReceiptNumber))
                        column.Item().Text($"Receipt {invoice.ReceiptNumber}");
                    if (!string.IsNullOrWhiteSpace(invoice.BillToName))
                        column.Item().Text($"Billed to: {invoice.BillToName}");
                    column.Item().Text($"Plan: {invoice.PlanName}");
                    column.Item().Text($"Status: {invoice.Status}");
                    column.Item().Text($"Currency: {invoice.Currency}");
                    column.Item().Text($"Amount: {amount} {invoice.Currency}");
                    column.Item().Text($"Tax: {CurrencyMath.Round(invoice.TaxAmount, invoice.Currency)} {invoice.Currency}");
                    column.Item().Text($"Period: {invoice.PeriodStart:yyyy-MM-dd} to {invoice.PeriodEnd:yyyy-MM-dd}");
                    if (invoice.DueDate.HasValue)
                        column.Item().Text($"Due: {invoice.DueDate:yyyy-MM-dd}");
                    column.Item().Text(invoice.PaidAt.HasValue
                        ? $"Paid: {invoice.PaidAt:yyyy-MM-dd}"
                        : "Unpaid");
                    column.Item().PaddingTop(12).Text("This document shows the currency charged by the payment provider. It is not a card receipt from the provider.");
                });
            });
        }).GeneratePdf();
    }
}

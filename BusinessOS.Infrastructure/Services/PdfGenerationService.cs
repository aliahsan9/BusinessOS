using BusinessOS.Application.Features.Pdf.Models;
using BusinessOS.Application.Features.Pdf.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BusinessOS.Infrastructure.Services;

public sealed class PdfGenerationService : IPdfGenerationService
{
    private const string PrimaryColor = "#1e3a5f";
    private const string AccentColor = "#2563eb";
    private const string MutedColor = "#64748b";

    static PdfGenerationService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateBusinessSummary(BusinessSummaryPdfModel model) =>
        RenderDocument("Business Summary Report", model.Header, content =>
        {
            content.Item().Text($"Report Date: {model.ReportDate:MMMM dd, yyyy}").FontSize(10).FontColor(MutedColor);
            content.Item().PaddingTop(4).Text($"Period: {model.PeriodLabel}").FontSize(10).FontColor(MutedColor);

            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Key Statistics"));
            content.Item().PaddingTop(8).Element(c => KeyValueGrid(c, model.Statistics));

            foreach (var chart in model.Charts)
            {
                content.Item().PaddingTop(16).Element(c => SectionTitle(c, chart.Title));
                content.Item().PaddingTop(8).Element(c => ChartDataTable(c, chart));
            }
        });

    public byte[] GenerateRevenueReport(RevenueReportPdfModel model) =>
        RenderDocument("Revenue Report", model.Header, content =>
        {
            content.Item().Text($"Period: {model.PeriodLabel}").FontSize(10).FontColor(MutedColor);
            content.Item().PaddingTop(8).Text($"Total Revenue: {FormatCurrency(model.TotalRevenue, model.Header.Currency)}")
                .Bold().FontSize(14).FontColor(PrimaryColor);

            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Revenue by Month"));
            content.Item().PaddingTop(8).Element(c => DataTable(c, ["Month", "Revenue"], model.RevenueByMonth, model.Header.Currency));

            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Revenue by Customer"));
            content.Item().PaddingTop(8).Element(c => DataTable(c, ["Customer", "Revenue"], model.RevenueByCustomer, model.Header.Currency));

            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Revenue by Project"));
            content.Item().PaddingTop(8).Element(c => DataTable(c, ["Project", "Revenue"], model.RevenueByProject, model.Header.Currency));
        });

    public byte[] GenerateExpenseReport(ExpenseReportPdfModel model) =>
        RenderDocument("Expense Report", model.Header, content =>
        {
            content.Item().Text($"Period: {model.PeriodLabel}").FontSize(10).FontColor(MutedColor);
            content.Item().PaddingTop(8).Text($"Total Expenses: {FormatCurrency(model.TotalExpenses, model.Header.Currency)}")
                .Bold().FontSize(14).FontColor(PrimaryColor);

            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Expense Categories"));
            content.Item().PaddingTop(8).Element(c => DataTable(c, ["Category", "Amount", "Count"], model.ByCategory, model.Header.Currency));

            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Monthly Expenses"));
            content.Item().PaddingTop(8).Element(c => DataTable(c, ["Month", "Amount"], model.MonthlyExpenses, model.Header.Currency));

            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Expense Trends"));
            content.Item().PaddingTop(8).Element(c => DataTable(c, ["Period", "Amount"], model.Trends, model.Header.Currency));
        });

    public byte[] GenerateProfitLossReport(ProfitLossPdfModel model) =>
        RenderDocument("Profit & Loss Statement", model.Header, content =>
        {
            content.Item().Text($"Period: {model.PeriodLabel}").FontSize(10).FontColor(MutedColor);
            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Statement Summary"));

            content.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                });

                PnlRow(table, "Total Revenue", FormatCurrency(model.TotalRevenue, model.Header.Currency), false);
                PnlRow(table, "Total Expenses", FormatCurrency(model.TotalExpenses, model.Header.Currency), false);
                table.Cell().ColumnSpan(2).PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                PnlRow(table, "Net Profit", FormatCurrency(model.NetProfit, model.Header.Currency), true);
                PnlRow(table, "Profit Margin", $"{model.ProfitMarginPercent:N2}%", true);
            });

            if (model.PeriodBreakdown.Count > 0)
            {
                content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Period Breakdown"));
                content.Item().PaddingTop(8).Element(c => DataTable(c, ["Period", "Amount"], model.PeriodBreakdown, model.Header.Currency));
            }
        });

    public byte[] GenerateCustomerReport(CustomerReportPdfModel model) =>
        RenderDocument("Customer Report", model.Header, content =>
        {
            content.Item().Text($"Period: {model.PeriodLabel}").FontSize(10).FontColor(MutedColor);
            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Customer Overview"));
            content.Item().PaddingTop(8).Element(c => DataTable(
                c,
                ["Customer", "Projects", "Revenue", "Outstanding", "Since"],
                model.Customers,
                model.Header.Currency));
        });

    public byte[] GenerateProjectReport(ProjectReportPdfModel model) =>
        RenderDocument("Project Report", model.Header, content =>
        {
            content.Item().PaddingTop(8).Element(c => SectionTitle(c, "Project Information"));
            content.Item().PaddingTop(8).Element(c => KeyValueGrid(c, model.ProjectInfo));

            if (model.AssignedMembers.Count > 0)
            {
                content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Assigned Members"));
                content.Item().PaddingTop(8).Text(string.Join(", ", model.AssignedMembers)).FontSize(10);
            }

            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Tasks"));
            content.Item().PaddingTop(8).Element(c => DataTable(c, ["Task", "Qty", "Unit Price", "Total"], model.Tasks, model.Header.Currency));
        });

    public byte[] GenerateTaskReport(TaskReportPdfModel model) =>
        RenderDocument("Task Productivity Report", model.Header, content =>
        {
            content.Item().Text($"Period: {model.PeriodLabel}").FontSize(10).FontColor(MutedColor);
            content.Item().PaddingTop(16).Element(c => SectionTitle(c, "Task Summary"));

            content.Item().PaddingTop(8).Element(c => KeyValueGrid(c,
            [
                new PdfKeyValueRow { Label = "Completed Tasks", Value = model.CompletedTasks.ToString() },
                new PdfKeyValueRow { Label = "Pending Tasks", Value = model.PendingTasks.ToString() },
                new PdfKeyValueRow { Label = "Overdue Tasks", Value = model.OverdueTasks.ToString() },
                new PdfKeyValueRow { Label = "Completion Rate", Value = $"{model.CompletionRate:N1}%" }
            ]));
        });

    public byte[] GenerateInvoice(InvoicePdfModel model) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => InvoiceHeader(c, model));
                page.Content().PaddingTop(20).Element(c => InvoiceBody(c, model));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

    private static byte[] RenderDocument(
        string title,
        BusinessHeaderInfo header,
        Action<ColumnDescriptor> buildContent)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ReportHeader(c, header, title));
                page.Content().PaddingTop(16).Column(buildContent);
                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(MutedColor));
                    text.Span($"{header.BusinessName} — ");
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void ReportHeader(IContainer container, BusinessHeaderInfo header, string title)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(header.BusinessName).Bold().FontSize(18).FontColor(PrimaryColor);
                    if (!string.IsNullOrWhiteSpace(header.Address))
                        left.Item().Text(header.Address).FontSize(9).FontColor(MutedColor);
                    if (!string.IsNullOrWhiteSpace(header.Email))
                        left.Item().Text(header.Email).FontSize(9).FontColor(MutedColor);
                    if (!string.IsNullOrWhiteSpace(header.Phone))
                        left.Item().Text(header.Phone).FontSize(9).FontColor(MutedColor);
                });

                row.ConstantItem(180).AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text(title).Bold().FontSize(14).FontColor(AccentColor);
                    right.Item().AlignRight().PaddingTop(4).Text($"Owner: {header.OwnerName}").FontSize(9).FontColor(MutedColor);
                });
            });

            column.Item().PaddingTop(12).LineHorizontal(2).LineColor(AccentColor);
        });
    }

    private static void InvoiceHeader(IContainer container, InvoicePdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(model.Header.BusinessName).Bold().FontSize(20).FontColor(PrimaryColor);
                    if (!string.IsNullOrWhiteSpace(model.Header.Address))
                        left.Item().Text(model.Header.Address).FontSize(9).FontColor(MutedColor);
                    if (!string.IsNullOrWhiteSpace(model.Header.Email))
                        left.Item().Text(model.Header.Email).FontSize(9).FontColor(MutedColor);
                    if (!string.IsNullOrWhiteSpace(model.Header.Phone))
                        left.Item().Text(model.Header.Phone).FontSize(9).FontColor(MutedColor);
                });

                row.ConstantItem(200).AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text("INVOICE").Bold().FontSize(22).FontColor(AccentColor);
                    right.Item().AlignRight().PaddingTop(4).Text(model.InvoiceNumber).FontSize(12).Bold();
                    right.Item().AlignRight().Text($"Status: {model.Status}").FontSize(9).FontColor(MutedColor);
                });
            });

            column.Item().PaddingTop(16).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            column.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().Column(billTo =>
                {
                    billTo.Item().Text("Bill To").Bold().FontSize(10).FontColor(PrimaryColor);
                    billTo.Item().PaddingTop(4).Text(model.CustomerName).Bold();
                    if (!string.IsNullOrWhiteSpace(model.CustomerEmail))
                        billTo.Item().Text(model.CustomerEmail).FontSize(9).FontColor(MutedColor);
                    if (!string.IsNullOrWhiteSpace(model.CustomerAddress))
                        billTo.Item().Text(model.CustomerAddress).FontSize(9).FontColor(MutedColor);
                });

                row.ConstantItem(180).AlignRight().Column(dates =>
                {
                    dates.Item().AlignRight().Text($"Invoice Date: {model.InvoiceDate:MMM dd, yyyy}");
                    dates.Item().AlignRight().Text($"Due Date: {model.DueDate:MMM dd, yyyy}");
                });
            });
        });
    }

    private static void InvoiceBody(IContainer container, InvoicePdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                });

                TableHeaderCell(table, "Description");
                TableHeaderCell(table, "Qty");
                TableHeaderCell(table, "Unit Price");
                TableHeaderCell(table, "Total");

                var rowIndex = 0;
                foreach (var item in model.LineItems)
                {
                    var bg = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                    TableBodyCell(table, item.Description, bg);
                    TableBodyCell(table, item.Quantity.ToString("N2"), bg);
                    TableBodyCell(table, FormatCurrency(item.UnitPrice, model.Header.Currency), bg);
                    TableBodyCell(table, FormatCurrency(item.Total, model.Header.Currency), bg);
                    rowIndex++;
                }
            });

            column.Item().PaddingTop(20).AlignRight().Width(240).Column(totals =>
            {
                TotalsRow(totals, "Subtotal", FormatCurrency(model.SubTotal, model.Header.Currency));
                TotalsRow(totals, "Discount", FormatCurrency(model.Discount, model.Header.Currency));
                TotalsRow(totals, "Tax", FormatCurrency(model.Tax, model.Header.Currency));
                totals.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                totals.Item().Row(row =>
                {
                    row.RelativeItem().Text("Grand Total").Bold().FontSize(12);
                    row.ConstantItem(100).AlignRight().Text(FormatCurrency(model.GrandTotal, model.Header.Currency)).Bold().FontSize(12).FontColor(PrimaryColor);
                });
                TotalsRow(totals, "Amount Paid", FormatCurrency(model.AmountPaid, model.Header.Currency));
                totals.Item().Row(row =>
                {
                    row.RelativeItem().Text("Outstanding").Bold();
                    row.ConstantItem(100).AlignRight().Text(FormatCurrency(model.OutstandingAmount, model.Header.Currency)).Bold().FontColor(model.OutstandingAmount > 0 ? Colors.Red.Medium : Colors.Green.Medium);
                });
            });

            if (!string.IsNullOrWhiteSpace(model.Notes))
            {
                column.Item().PaddingTop(20).Column(notes =>
                {
                    notes.Item().Text("Notes").Bold().FontColor(PrimaryColor);
                    notes.Item().PaddingTop(4).Text(model.Notes!).FontSize(9).FontColor(MutedColor);
                });
            }
        });
    }

    private static void SectionTitle(IContainer container, string title) =>
        container.Text(title).Bold().FontSize(12).FontColor(PrimaryColor);

    private static void KeyValueGrid(IContainer container, IReadOnlyList<PdfKeyValueRow> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            for (var i = 0; i < rows.Count; i += 2)
            {
                StatCell(table, rows[i]);
                if (i + 1 < rows.Count)
                    StatCell(table, rows[i + 1]);
                else
                    table.Cell();
            }
        });
    }

    private static void StatCell(TableDescriptor table, PdfKeyValueRow row)
    {
        table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text(row.Label).FontSize(9).FontColor(MutedColor);
            col.Item().PaddingTop(4).Text(row.Value).Bold().FontSize(14).FontColor(PrimaryColor);
        });
    }

    private static void ChartDataTable(IContainer container, PdfChartSnapshot chart)
    {
        var rows = chart.Labels
            .Select((label, i) => new PdfTableRow
            {
                Cells = [label, i < chart.Values.Count ? chart.Values[i].ToString("N2") : "0"]
            })
            .ToList();

        DataTable(container, ["Label", "Value"], rows, "USD");
    }

    private static void DataTable(
        IContainer container,
        string[] headers,
        IReadOnlyList<PdfTableRow> rows,
        string currency)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (var i = 0; i < headers.Length; i++)
                    columns.RelativeColumn();
            });

            foreach (var header in headers)
                TableHeaderCell(table, header);

            var rowIndex = 0;
            foreach (var row in rows)
            {
                var bg = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                for (var i = 0; i < headers.Length; i++)
                {
                    var cell = i < row.Cells.Count ? row.Cells[i] : string.Empty;
                    if (i == headers.Length - 1 && headers[^1] is "Revenue" or "Amount" or "Total" or "Outstanding")
                        cell = cell.StartsWith('$') || cell.Contains('%') ? cell : FormatCurrency(decimal.TryParse(cell, out var d) ? d : 0, currency);
                    TableBodyCell(table, cell, bg);
                }
                rowIndex++;
            }
        });
    }

    private static void TableHeaderCell(TableDescriptor table, string text) =>
        table.Cell().Background(PrimaryColor).Padding(8)
            .Text(text).FontColor(Colors.White).Bold().FontSize(9);

    private static void TableBodyCell(TableDescriptor table, string text, string bg) =>
        table.Cell().Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(8)
            .Text(text).FontSize(9);

    private static void PnlRow(TableDescriptor table, string label, string value, bool bold)
    {
        var style = bold ? TextStyle.Default.Bold() : TextStyle.Default;
        table.Cell().Padding(6).Text(label).Style(style);
        table.Cell().Padding(6).AlignRight().Text(value).Style(style);
    }

    private static void TotalsRow(ColumnDescriptor column, string label, string value)
    {
        column.Item().PaddingVertical(2).Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(10);
            row.ConstantItem(100).AlignRight().Text(value).FontSize(10);
        });
    }

    private static string FormatCurrency(decimal amount, string currency) =>
        currency switch
        {
            "EUR" => amount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("de-DE")),
            "GBP" => amount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-GB")),
            _ => amount.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
        };
}

using System.Text.Json;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Help.Services;
using BusinessOS.Application.Features.Pdf.Models;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;

namespace BusinessOS.UnitTests.Services;

public class PdfGenerationServiceTests
{
    private readonly PdfGenerationService _service = new();

    [Fact]
    public void Reports_RenderDistinctPdfDocuments()
    {
        var header = new BusinessHeaderInfo
        {
            BusinessName = "Northwind Traders",
            OwnerName = "Ada",
            Currency = "USD"
        };

        var summary = _service.GenerateBusinessSummary(new BusinessSummaryPdfModel
        {
            Header = header,
            ReportDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodLabel = "March",
            Statistics = [new PdfKeyValueRow { Label = "Revenue", Value = "100.00" }],
            Charts =
            [
                new PdfChartSnapshot
                {
                    Title = "Sales",
                    Labels = ["Mon"],
                    Values = [10m]
                }
            ]
        });

        var invoiceA = _service.GenerateInvoice(Invoice(header, "INV-1", 25.50m));
        var invoiceB = _service.GenerateInvoice(Invoice(header, "INV-2", 10m));
        var revenue = _service.GenerateRevenueReport(new RevenueReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Year",
            TotalRevenue = 100.25m,
            RevenueByMonth = [new PdfTableRow { Cells = ["March", "100.25"] }]
        });
        var expenses = _service.GenerateExpenseReport(new ExpenseReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Year",
            TotalExpenses = 40.10m,
            ByCategory = [new PdfTableRow { Cells = ["Rent", "40.10"] }]
        });
        var profit = _service.GenerateProfitLossReport(new ProfitLossPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Year",
            TotalRevenue = 100.25m,
            TotalExpenses = 40.10m,
            NetProfit = 60.15m,
            ProfitMarginPercent = 60m
        });
        var customers = _service.GenerateCustomerReport(new CustomerReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Year",
            Customers = [new PdfTableRow { Cells = ["Ada", "1"] }]
        });
        var project = _service.GenerateProjectReport(new ProjectReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            ProjectInfo = [new PdfKeyValueRow { Label = "Name", Value = "Rollout" }],
            Tasks = [new PdfTableRow { Cells = ["Ship", "Done"] }],
            AssignedMembers = ["Ada"]
        });
        var tasks = _service.GenerateTaskReport(new TaskReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Week",
            CompletedTasks = 2,
            PendingTasks = 1,
            OverdueTasks = 0,
            CompletionRate = 66.67m
        });

        foreach (var pdf in new[] { summary, invoiceA, revenue, expenses, profit, customers, project, tasks })
        {
            pdf.Length.Should().BeGreaterThan(400);
            System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        }

        invoiceA.Should().NotEqual(invoiceB);
    }

    private static InvoicePdfModel Invoice(BusinessHeaderInfo header, string number, decimal total) =>
        new()
        {
            Header = header,
            InvoiceNumber = number,
            InvoiceDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            Status = "Sent",
            CustomerName = "Ada Lovelace",
            LineItems =
            [
                new InvoiceLineItem { Description = "Laptop", Quantity = 1, UnitPrice = total, Total = total }
            ],
            SubTotal = total,
            GrandTotal = total,
            OutstandingAmount = total
        };
}

public class HelpAndToolSchemaTests
{
    [Fact]
    public async Task HelpCenter_HasStableTopics_WithoutSecrets()
    {
        var help = await new HelpService().GetHelpCenterAsync();

        help.Faqs.Should().NotBeEmpty();
        help.Faqs.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        help.Faqs.Should().Contain(x => x.Category == "Customers");
        string.Join(' ', help.Faqs.Select(x => x.Answer)).Should().NotContain("Password=");
        string.Join(' ', help.Faqs.Select(x => x.Answer)).Should().NotContain("Server=");
    }

    [Fact]
    public void ToolSchemas_AreJson_AndMutationsNameTheirTargets()
    {
        foreach (var tool in Enum.GetValues<AiToolName>())
        {
            var schema = AgentToolSchemas.For(tool);
            if (schema is null)
                continue;

            using var document = JsonDocument.Parse(schema);
            document.RootElement.GetProperty("type").GetString().Should().Be("object");
        }

        AgentToolSchemas.For(AiToolName.CreateCustomer).Should().Contain("firstName");
        AgentToolSchemas.For(AiToolName.DeleteCustomer).Should().Contain("customerId");
        AgentToolSchemas.For(AiToolName.DeleteProduct).Should().Contain("productId");
        AgentToolSchemas.For(AiToolName.CreateSale).Should().Contain("quantity");
    }
}

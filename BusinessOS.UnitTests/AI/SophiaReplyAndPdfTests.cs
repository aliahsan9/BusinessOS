using System.Text;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.Help.Services;
using BusinessOS.Application.Features.Pdf.Models;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class SophiaReplyBuilderTests
{
    [Theory]
    [InlineData("thanks", "You're welcome")]
    [InlineData("shukriya", "You're welcome")]
    [InlineData("goodbye", "Take care")]
    [InlineData("hi", "What should we tackle")]
    [InlineData("سلام", "What should we tackle")]
    public void ConversationalReply_HandlesGreetingAndThanks(string message, string expected)
    {
        AiNaturalReplyBuilder.BuildConversationalReply(message, new AiPageContextDto())
            .Should().Contain(expected);
    }

    [Fact]
    public void ConversationalReply_UsesTheOpenPage()
    {
        var customer = AiNaturalReplyBuilder.BuildConversationalReply(
            "what can you do",
            new AiPageContextDto { Module = "customers", CustomerId = Guid.NewGuid() });
        customer.Should().Contain("customer");

        var invoice = AiNaturalReplyBuilder.BuildConversationalReply(
            "what can you do",
            new AiPageContextDto { Module = "invoices", InvoiceId = Guid.NewGuid() });
        invoice.Should().Contain("invoice");

        var purchases = AiNaturalReplyBuilder.BuildConversationalReply(
            "what can you do",
            new AiPageContextDto { Module = "suppliers" });
        purchases.Should().Contain("Purchases");
    }

    [Theory]
    [InlineData("how can I increase laptop sales", "laptop")]
    [InlineData("tips to grow our revenue", "pipeline")]
    [InlineData("how should I improve customer retention", "retention")]
    [InlineData("best way to grow my business", "Happy to help")]
    public void AdviceReply_StaysOnTheAskedTopic(string message, string expected)
    {
        AiNaturalReplyBuilder.BuildAdviceReply(message).Should().Contain(expected);
    }

    [Theory]
    [InlineData("how do I add a customer", "New Customer")]
    [InlineData("how do orders work", "Orders")]
    [InlineData("how do I create an invoice", "completed orders")]
    [InlineData("how do tasks work", "line items")]
    [InlineData("what is this app", "Welcome to BusinessOS")]
    public void HelpReply_PointsAtTheMatchingScreen(string message, string expected)
    {
        AiNaturalReplyBuilder.BuildHelpReply(message).Should().Contain(expected);
    }

    [Fact]
    public void BusinessReply_SummarizesOnlyTheSuppliedTenantContext()
    {
        var context = new AiContextDto
        {
            User = new AiUserContextDto { UserId = "user-1" },
            Page = new AiPageContextDto(),
            Customer = new
            {
                fullName = "Ada Lovelace",
                email = "ada@test.com",
                phoneNumber = "123",
                city = "London",
                country = "UK",
                isActive = true
            },
            Analytics = new
            {
                totalOrders = 2,
                totalSpending = 80.5m,
                averageOrderValue = 40.25m,
                totalCompletedOrders = 1,
                lastOrderDate = "2026-03-02"
            },
            Orders = [new { orderNumber = "ORD-1", status = "Completed", grandTotal = 80.5m }],
            Invoices = [new { invoiceNumber = "INV-1", status = "Sent", outstandingAmount = 10m }]
        };

        var summary = AiNaturalReplyBuilder.BuildBusinessReply("summarize this customer", context);
        summary.Should().Contain("Ada Lovelace");
        summary.Should().Contain("ORD-1");
        summary.Should().Contain("INV-1");
        summary.Should().NotContain("deleted successfully");

        AiNaturalReplyBuilder.BuildBusinessReply("what is their revenue", context)
            .Should().Contain("Ada Lovelace")
            .And.Contain("80.50");

        var empty = new AiContextDto
        {
            User = new AiUserContextDto { UserId = "user-1" },
            Page = new AiPageContextDto()
        };
        AiNaturalReplyBuilder.BuildBusinessReply("show everything", empty)
            .Should().Contain("couldn't find relevant business data");
    }

    [Fact]
    public void BusinessReply_OverdueQuestion_DoesNotInventInvoices()
    {
        var context = new AiContextDto
        {
            User = new AiUserContextDto { UserId = "user-1" },
            Page = new AiPageContextDto(),
            Invoices = []
        };

        AiNaturalReplyBuilder.BuildBusinessReply("show overdue invoices", context)
            .Should().NotContain("successfully");
    }
}

public class PdfAndHelpTests
{
    [Fact]
    public void PdfReports_AreDistinctDocuments()
    {
        var service = new PdfGenerationService();
        var header = new BusinessHeaderInfo
        {
            BusinessName = "Northwind Test",
            OwnerName = "Ada",
            Currency = "USD",
            Email = "ada@test.com"
        };

        var summary = service.GenerateBusinessSummary(new BusinessSummaryPdfModel
        {
            Header = header,
            ReportDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodLabel = "March",
            Statistics = [new PdfKeyValueRow { Label = "Revenue", Value = "100.00" }],
            Charts = [new PdfChartSnapshot { Title = "Sales", Labels = ["Jan"], Values = [10] }]
        });
        var invoiceA = service.GenerateInvoice(Invoice("INV-1", header));
        var invoiceB = service.GenerateInvoice(Invoice("INV-2", header));
        var revenue = service.GenerateRevenueReport(new RevenueReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Year",
            TotalRevenue = 100.12m,
            RevenueByMonth = [new PdfTableRow { Cells = ["March", "100.12"] }]
        });
        var expenses = service.GenerateExpenseReport(new ExpenseReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Year",
            TotalExpenses = 40,
            ByCategory = [new PdfTableRow { Cells = ["Rent", "40.00"] }]
        });
        var profit = service.GenerateProfitLossReport(new ProfitLossPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Year",
            TotalRevenue = 100,
            TotalExpenses = 40,
            NetProfit = 60,
            ProfitMarginPercent = 60
        });
        var customers = service.GenerateCustomerReport(new CustomerReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Year",
            Customers = [new PdfTableRow { Cells = ["Ada", "1"] }]
        });
        var projects = service.GenerateProjectReport(new ProjectReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            ProjectInfo = [new PdfKeyValueRow { Label = "Name", Value = "Alpha" }],
            Tasks = [new PdfTableRow { Cells = ["Design", "Open"] }],
            AssignedMembers = ["Ada"]
        });
        var tasks = service.GenerateTaskReport(new TaskReportPdfModel
        {
            Header = header,
            ReportDate = DateTime.UtcNow,
            PeriodLabel = "Year",
            CompletedTasks = 1,
            PendingTasks = 2,
            OverdueTasks = 0,
            CompletionRate = 33.33m
        });

        foreach (var pdf in new[] { summary, invoiceA, revenue, expenses, profit, customers, projects, tasks })
        {
            Encoding.ASCII.GetString(pdf[..5]).Should().Be("%PDF-");
            pdf.Length.Should().BeGreaterThan(400);
        }

        invoiceA.Should().NotEqual(invoiceB);
    }

    [Fact]
    public async Task HelpCenter_ReturnsStableTopicsWithoutSecrets()
    {
        var help = await new HelpService().GetHelpCenterAsync();

        help.Faqs.Should().NotBeEmpty();
        help.Faqs.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        help.Faqs.Should().Contain(x => x.Category == "Customers");
        string.Join(' ', help.Faqs.Select(x => x.Answer)).Should().NotContain("Password=");
        string.Join(' ', help.Faqs.Select(x => x.Answer)).Should().NotContain("Server=");
    }

    private static InvoicePdfModel Invoice(string number, BusinessHeaderInfo header) =>
        new()
        {
            Header = header,
            InvoiceNumber = number,
            InvoiceDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            Status = "Sent",
            CustomerName = "Ada Lovelace",
            CustomerEmail = "ada@test.com",
            LineItems =
            [
                new InvoiceLineItem { Description = "Laptop", Quantity = 2, UnitPrice = 10.5m, Total = 21 }
            ],
            SubTotal = 21,
            Discount = 1,
            Tax = 0.5m,
            GrandTotal = 20.5m,
            AmountPaid = 0,
            OutstandingAmount = 20.5m,
            Notes = "Net 14"
        };
}

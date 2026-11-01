using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.AI.Copilot.Tools;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.UnitTests.AI;

public class SophiaInvoiceBalanceTests
{
    [Fact]
    public void ApplyStoredBalance_PartialPaymentPastDue_BecomesOverdue()
    {
        var invoice = new Invoice
        {
            GrandTotal = 100,
            Status = InvoiceStatusNames.Sent,
            DueDate = DateTime.UtcNow.AddDays(-2),
            AmountPaid = 0,
            OutstandingAmount = 100
        };

        InvoicePaymentCalculator.ApplyStoredBalance(invoice, 40, DateTime.UtcNow);

        invoice.AmountPaid.Should().Be(40);
        invoice.OutstandingAmount.Should().Be(60);
        invoice.Status.Should().Be(InvoiceStatusNames.Overdue);
    }

    [Fact]
    public void ApplyStoredBalance_FullPayment_BecomesPaid_AndPaidStatusIsNeverOverdue()
    {
        var settled = new Invoice
        {
            GrandTotal = 80,
            Status = InvoiceStatusNames.Sent,
            DueDate = DateTime.UtcNow.AddDays(-5)
        };
        InvoicePaymentCalculator.ApplyStoredBalance(settled, 80, DateTime.UtcNow);
        settled.Status.Should().Be(InvoiceStatusNames.Paid);
        settled.OutstandingAmount.Should().Be(0);

        var markedPaid = new Invoice
        {
            GrandTotal = 80,
            Status = InvoiceStatusNames.Paid,
            DueDate = DateTime.UtcNow.AddDays(-5),
            OutstandingAmount = 80
        };
        InvoicePaymentCalculator.ApplyStoredBalance(markedPaid, 0, DateTime.UtcNow);
        markedPaid.Status.Should().Be(InvoiceStatusNames.Paid);
        markedPaid.OutstandingAmount.Should().Be(80);
        InvoicePaymentCalculator.IsCollectibleOverdue(
            markedPaid.Status,
            markedPaid.OutstandingAmount,
            markedPaid.DueDate,
            DateTime.UtcNow).Should().BeFalse();
        InvoicePaymentCalculator.IsPaidStatusWithOpenBalance(markedPaid.Status, markedPaid.OutstandingAmount)
            .Should().BeTrue();
    }

    [Fact]
    public void Outstanding_DoesNotGoNegativeWhenPaymentsExceedTheTotal()
    {
        InvoicePaymentCalculator.Outstanding(50, 80).Should().Be(0);
    }

    [Fact]
    public async Task GetInvoices_ExcludesPaidAndUsesPaymentBalances()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var openOrderId = Guid.NewGuid();
        var paidOrderId = Guid.NewGuid();
        var partialOrderId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Customers.Add(Customer(tenantId, customerId));
        context.Orders.AddRange(
            Order(tenantId, openOrderId, customerId, 100, now.AddDays(-10)),
            Order(tenantId, paidOrderId, customerId, 100, now.AddDays(-10)),
            Order(tenantId, partialOrderId, customerId, 100, now.AddDays(-10)));
        context.Invoices.AddRange(
            InvoiceRow(tenantId, openOrderId, customerId, "INV-OPEN", 100, InvoiceStatusNames.Sent, now.AddDays(-3)),
            InvoiceRow(tenantId, paidOrderId, customerId, "INV-PAID", 100, InvoiceStatusNames.Paid, now.AddDays(-3)),
            InvoiceRow(tenantId, partialOrderId, customerId, "INV-PART", 100, InvoiceStatusNames.Sent, now.AddDays(-3)));
        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = partialOrderId,
            CustomerId = customerId,
            Amount = 25,
            RefundedAmount = 0,
            Status = PaymentStatusNames.Completed,
            PaymentMethod = "Cash",
            PaymentDate = now
        });
        await context.SaveChangesAsync();

        var result = await new GetInvoicesTool(context).ExecuteAsync(Context("show overdue invoices"));

        var parts = result.Summary.Split("These invoices are marked Paid");
        parts[0].Should().Contain("INV-OPEN");
        parts[0].Should().Contain("INV-PART");
        parts[0].Should().Contain("25");
        parts[0].Should().NotContain("INV-PAID");
        parts.Should().HaveCount(2);
        parts[1].Should().Contain("INV-PAID");
    }

    [Fact]
    public async Task GetInvoices_EmptyWorkspace_DoesNotInventBalances()
    {
        var (context, _, _) = InMemoryDbContextFactory.Create();
        var result = await new GetInvoicesTool(context).ExecuteAsync(Context("show overdue invoices"));
        result.Summary.Should().Contain("No invoices found");
        result.Summary.Should().NotContain("$");
    }

    [Fact]
    public async Task GetInvoices_DoesNotReadAnotherTenantsInvoices()
    {
        var database = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(database)
            .Options;

        var providerA = new TenantProvider();
        providerA.SetTenantId(tenantA);
        await using (var contextA = new BusinessOSDbContext(options, providerA))
        {
            var customerId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            contextA.Customers.Add(Customer(tenantA, customerId));
            contextA.Orders.Add(Order(tenantA, orderId, customerId, 40, DateTime.UtcNow.AddDays(-8)));
            contextA.Invoices.Add(InvoiceRow(
                tenantA, orderId, customerId, "INV-SECRET", 40, InvoiceStatusNames.Sent, DateTime.UtcNow.AddDays(-2)));
            await contextA.SaveChangesAsync();
        }

        var providerB = new TenantProvider();
        providerB.SetTenantId(tenantB);
        await using var contextB = new BusinessOSDbContext(options, providerB);
        var result = await new GetInvoicesTool(contextB).ExecuteAsync(Context("show overdue invoices"));
        result.Summary.Should().Contain("No invoices found");
        result.Summary.Should().NotContain("INV-SECRET");
    }

    [Fact]
    public void CancelAndAmbiguousQuestions_DoNotLookLikeAProductSearch()
    {
        AiNaturalReplyBuilder.BuildConversationalReply("Cancel", new AiPageContextDto())
            .Should().Contain("nothing waiting to cancel");
        AiNaturalReplyBuilder.BuildConversationalReply("What is the weather like?", new AiPageContextDto(), language: "ur-Latn")
            .Should().Contain("kaun sa kaam");
    }

    private static AiCopilotExecutionContext Context(string message) => new()
    {
        Message = message,
        Intent = AiCopilotIntent.BusinessIntelligence,
        Page = new AiPageContextDto(),
        Memory = new AiMemoryStateDto(),
        Request = new AiChatRequest(message),
        SessionId = Guid.NewGuid()
    };

    private static Customer Customer(Guid tenantId, Guid id) => new()
    {
        Id = id,
        TenantId = tenantId,
        FirstName = "Asim",
        LastName = "Khan",
        Email = $"{id:N}@example.com",
        PhoneNumber = "03291327355",
        Address = "Lahore",
        City = "Lahore",
        Country = "Pakistan",
        PostalCode = "54000"
    };

    private static Order Order(Guid tenantId, Guid id, Guid customerId, decimal total, DateTime when) => new()
    {
        Id = id,
        TenantId = tenantId,
        CustomerId = customerId,
        OrderNumber = id.ToString()[..8],
        OrderDate = when,
        Status = "Completed",
        GrandTotal = total
    };

    private static Invoice InvoiceRow(
        Guid tenantId,
        Guid orderId,
        Guid customerId,
        string number,
        decimal total,
        string status,
        DateTime due) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        OrderId = orderId,
        CustomerId = customerId,
        InvoiceNumber = number,
        InvoiceDate = due.AddDays(-7),
        DueDate = due,
        Status = status,
        GrandTotal = total,
        AmountPaid = 0,
        OutstandingAmount = total
    };
}

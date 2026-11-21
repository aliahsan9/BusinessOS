using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.FinanceAgent;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Nalva;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class NalvaPhase5EFinanceTests
{
    [Fact]
    public async Task SalesRevenue_UsesCompletedOrders_NotUnpaidInvoices()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        await harness.SeedOrderAsync(customerId, 500m, OrderStatusNames.Completed);
        await harness.SeedOrderAsync(customerId, 200m, OrderStatusNames.Pending);
        var unpaidOrder = await harness.SeedOrderAsync(customerId, 999m, OrderStatusNames.Completed);
        await harness.SeedInvoiceAsync(customerId, unpaidOrder, 999m, amountPaid: 0m, outstanding: 999m, status: InvoiceStatusNames.Sent);

        var intel = await harness.Finance.GetIntelligenceAsync(period: "month");

        intel.SalesRevenue.Available.Should().BeTrue();
        intel.SalesRevenue.Value.Should().Be(500m + 999m);
        intel.SalesRevenue.Value.Should().NotBe(999m);
        intel.DataClassification.Should().Be("REAL");
    }

    [Fact]
    public async Task CollectedCash_NetsRefunds_IgnoresPendingPayments()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        var orderId = await harness.SeedOrderAsync(customerId, 300m, OrderStatusNames.Completed);
        await harness.SeedPaymentAsync(customerId, orderId, 200m, PaymentStatusNames.Completed, refunded: 50m);
        await harness.SeedPaymentAsync(customerId, orderId, 100m, PaymentStatusNames.Pending);
        await harness.SeedPaymentAsync(customerId, orderId, 75m, PaymentStatusNames.Completed, refunded: 0m);

        var intel = await harness.Finance.GetIntelligenceAsync(period: "month");

        intel.CollectedCash.Available.Should().BeTrue();
        intel.CollectedCash.Value.Should().Be(150m + 75m);
    }

    [Fact]
    public async Task Expenses_ExcludePendingRejectedAndPurchaseOrders()
    {
        var harness = await Harness.OpenAsync();
        var categoryId = await harness.SeedExpenseCategoryAsync("Ops");
        await harness.SeedExpenseAsync(categoryId, "Approved rent", 100m, ExpenseStatusNames.Approved);
        await harness.SeedExpenseAsync(categoryId, "Paid utilities", 40m, ExpenseStatusNames.Paid);
        await harness.SeedExpenseAsync(categoryId, "Pending travel", 500m, ExpenseStatusNames.Pending);
        await harness.SeedExpenseAsync(categoryId, "Rejected meal", 80m, ExpenseStatusNames.Rejected);
        var supplierId = await harness.SeedSupplierAsync("PO Co");
        await harness.SeedPurchaseAsync(supplierId, 10_000m);

        var intel = await harness.Finance.GetIntelligenceAsync(period: "month");
        var analysis = await harness.Finance.AnalyzeExpensesAsync(period: "month");

        intel.TotalExpenses.Value.Should().Be(140m);
        analysis.TotalRecognizedExpenses.Should().Be(140m);
        analysis.Limitations.Should().Contain(x => x.Contains("Pending/Rejected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GrossAndNetProfit_UnavailableWithoutCogs()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        await harness.SeedOrderAsync(customerId, 250m, OrderStatusNames.Completed, unitCost: 0m);
        var categoryId = await harness.SeedExpenseCategoryAsync("Ops");
        await harness.SeedExpenseAsync(categoryId, "Ads", 25m, ExpenseStatusNames.Approved);

        var intel = await harness.Finance.GetIntelligenceAsync(period: "month");

        intel.SalesRevenue.Value.Should().Be(250m);
        intel.CostOfGoodsSold.Available.Should().BeFalse();
        intel.CostOfGoodsSold.Value.Should().BeNull();
        intel.GrossProfit.Available.Should().BeFalse();
        intel.GrossProfit.Value.Should().BeNull();
        intel.NetProfit.Available.Should().BeFalse();
        intel.NetProfit.Value.Should().BeNull();
        intel.Limitations.Should().Contain(x => x.Contains("COGS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GrossAndNetProfit_WhenCogsPresent()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        await harness.SeedOrderAsync(customerId, 200m, OrderStatusNames.Completed, unitCost: 30m, quantity: 2m);
        var categoryId = await harness.SeedExpenseCategoryAsync("Ops");
        await harness.SeedExpenseAsync(categoryId, "Ads", 20m, ExpenseStatusNames.Paid);

        var intel = await harness.Finance.GetIntelligenceAsync(period: "month");

        intel.CostOfGoodsSold.Available.Should().BeTrue();
        intel.CostOfGoodsSold.Value.Should().Be(60m);
        intel.GrossProfit.Available.Should().BeTrue();
        intel.GrossProfit.Value.Should().Be(140m);
        intel.NetProfit.Available.Should().BeTrue();
        intel.NetProfit.Value.Should().Be(120m);
    }

    [Fact]
    public async Task PartialPayments_AppearInReceivablesOutstanding()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        var orderId = await harness.SeedOrderAsync(customerId, 400m, OrderStatusNames.Completed);
        await harness.SeedInvoiceAsync(customerId, orderId, 400m, amountPaid: 150m, outstanding: 250m, status: InvoiceStatusNames.PartiallyPaid);

        var ar = await harness.Finance.GetReceivablesAsync();

        ar.TotalOutstanding.Should().Be(250m);
        ar.Items.Should().ContainSingle(x => x.OutstandingAmount == 250m && x.AmountPaid == 150m);
    }

    [Fact]
    public async Task RefundedPayment_DoesNotCountAsCollectedCash()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        var orderId = await harness.SeedOrderAsync(customerId, 120m, OrderStatusNames.Completed);
        await harness.SeedPaymentAsync(customerId, orderId, 120m, PaymentStatusNames.Refunded);
        await harness.SeedPaymentAsync(customerId, orderId, 80m, PaymentStatusNames.Completed, refunded: 80m);

        var intel = await harness.Finance.GetIntelligenceAsync(period: "month");

        intel.CollectedCash.Value.Should().Be(0m);
    }

    [Fact]
    public async Task ReceivablesAging_BucketsByDaysOverdue()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        var today = DateTime.UtcNow.Date;
        var currentOrder = await harness.SeedOrderAsync(customerId, 50m, OrderStatusNames.Completed);
        var d15Order = await harness.SeedOrderAsync(customerId, 60m, OrderStatusNames.Completed);
        var d45Order = await harness.SeedOrderAsync(customerId, 70m, OrderStatusNames.Completed);
        var d75Order = await harness.SeedOrderAsync(customerId, 80m, OrderStatusNames.Completed);
        var d120Order = await harness.SeedOrderAsync(customerId, 90m, OrderStatusNames.Completed);

        await harness.SeedInvoiceAsync(customerId, currentOrder, 50m, 0m, 50m, InvoiceStatusNames.Sent, dueDate: today.AddDays(5));
        await harness.SeedInvoiceAsync(customerId, d15Order, 60m, 0m, 60m, InvoiceStatusNames.Overdue, dueDate: today.AddDays(-15));
        await harness.SeedInvoiceAsync(customerId, d45Order, 70m, 0m, 70m, InvoiceStatusNames.Overdue, dueDate: today.AddDays(-45));
        await harness.SeedInvoiceAsync(customerId, d75Order, 80m, 0m, 80m, InvoiceStatusNames.Overdue, dueDate: today.AddDays(-75));
        await harness.SeedInvoiceAsync(customerId, d120Order, 90m, 0m, 90m, InvoiceStatusNames.Overdue, dueDate: today.AddDays(-120));

        var ar = await harness.Finance.GetReceivablesAsync();

        ar.Aging.Should().Contain(x => x.Label == "Current" && x.Amount == 50m && x.Count == 1);
        ar.Aging.Should().Contain(x => x.Label == "1-30" && x.Amount == 60m && x.Count == 1);
        ar.Aging.Should().Contain(x => x.Label == "31-60" && x.Amount == 70m && x.Count == 1);
        ar.Aging.Should().Contain(x => x.Label == "61-90" && x.Amount == 80m && x.Count == 1);
        ar.Aging.Should().Contain(x => x.Label == "90+" && x.Amount == 90m && x.Count == 1);
    }

    [Fact]
    public async Task Payables_UsePurchaseInvoices_NotOpenPurchaseOrders()
    {
        var harness = await Harness.OpenAsync();
        var supplierId = await harness.SeedSupplierAsync("Supplier Pay");
        await harness.SeedPurchaseAsync(supplierId, 5_000m, status: PurchaseOrderStatusNames.Approved);
        await harness.SeedPurchaseInvoiceAsync(supplierId, "PI-1", total: 800m, amountPaid: 200m);

        var ap = await harness.Finance.GetPayablesAsync();
        var intel = await harness.Finance.GetIntelligenceAsync(period: "month");

        ap.TotalOutstanding.Should().Be(600m);
        intel.OutstandingPayables.Value.Should().Be(600m);
        ap.Limitations.Should().Contain(x => x.Contains("purchase orders", StringComparison.OrdinalIgnoreCase));
        ap.Items.Should().NotContain(x => x.Outstanding == 5_000m);
    }

    [Fact]
    public async Task DuplicateExpenseDetection()
    {
        var harness = await Harness.OpenAsync();
        var categoryId = await harness.SeedExpenseCategoryAsync("Travel");
        var when = DateTime.UtcNow.Date.AddDays(-1);
        await harness.SeedExpenseAsync(categoryId, "Hotel stay", 220m, ExpenseStatusNames.Approved, when, vendor: "Hilton");
        await harness.SeedExpenseAsync(categoryId, "Hotel stay", 220m, ExpenseStatusNames.Paid, when.AddDays(1), vendor: "Hilton");

        var analysis = await harness.Finance.AnalyzeExpensesAsync(period: "month");

        analysis.DuplicateHints.Should().NotBeEmpty();
        analysis.DuplicateHints.Should().Contain(x => x.Amount == 220m && x.Title == "Hotel stay");
    }

    [Fact]
    public async Task ExpenseCategorySuggestions_AreAdvisoryOnly()
    {
        var harness = await Harness.OpenAsync();
        var otherId = await harness.SeedExpenseCategoryAsync("Other");
        await harness.SeedExpenseCategoryAsync("Travel");
        await harness.SeedExpenseAsync(otherId, "Travel flight booking", 90m, ExpenseStatusNames.Approved, vendor: "Travel Co");

        var suggestions = await harness.Finance.SuggestExpenseCategoriesAsync();

        suggestions.Should().NotBeEmpty();
        suggestions.Should().OnlyContain(x => x.Applied == false);
        suggestions.Should().Contain(x => x.SuggestedCategoryName == "Travel");
    }

    [Fact]
    public async Task CashFlowProjection_UnavailableWithoutHistory()
    {
        var harness = await Harness.OpenAsync(seedOpeningBalance: true);

        var cf = await harness.Finance.GetCashFlowAsync(projectionDays: 14);

        cf.ProjectionAvailable.Should().BeFalse();
        cf.ProjectionConfidence.Should().Be("unavailable");
        cf.Projection.Should().BeEmpty();
        cf.Limitations.Should().Contain(x => x.Contains("projection unavailable", StringComparison.OrdinalIgnoreCase));
        cf.ClosingBalance.Should().NotBeNull();
        cf.DataClassification.Should().Be("REAL");
    }

    [Fact]
    public async Task CashFlowProjection_AvailableWithHistoryAndOpeningBalance()
    {
        var harness = await Harness.OpenAsync(seedOpeningBalance: true, openingCash: 1_000m);
        var customerId = await harness.SeedCustomerAsync();
        var orderId = await harness.SeedOrderAsync(customerId, 100m, OrderStatusNames.Completed);
        await harness.SeedPaymentAsync(customerId, orderId, 100m, PaymentStatusNames.Completed, when: DateTime.UtcNow.AddMonths(-1));
        await harness.SeedPaymentAsync(customerId, orderId, 50m, PaymentStatusNames.Completed, when: DateTime.UtcNow);
        var categoryId = await harness.SeedExpenseCategoryAsync("Ops");
        await harness.SeedExpenseAsync(categoryId, "Prior rent", 30m, ExpenseStatusNames.Paid, DateTime.UtcNow.AddMonths(-2));

        var cf = await harness.Finance.GetCashFlowAsync(projectionDays: 14);

        cf.OpeningBalanceConfigured.Should().BeTrue();
        cf.OpeningBalance.Should().Be(1_000m);
        cf.ClosingBalanceAvailable.Should().BeTrue();
        cf.ProjectionAvailable.Should().BeTrue();
        cf.Projection.Should().NotBeEmpty();
        cf.ProjectionConfidence.Should().NotBe("unavailable");
    }

    [Fact]
    public async Task BudgetAlert_WhenOverspent()
    {
        var harness = await Harness.OpenAsync();
        var categoryId = await harness.SeedExpenseCategoryAsync("Marketing");
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        await harness.Finance.UpsertBudgetAsync(new FinanceBudgetWriteRequest
        {
            Name = "Marketing budget",
            ExpenseCategoryId = categoryId,
            Amount = 100m,
            PeriodStart = monthStart,
            PeriodEnd = monthStart.AddMonths(1).AddTicks(-1)
        });
        await harness.SeedExpenseAsync(categoryId, "Ads burst", 150m, ExpenseStatusNames.Approved);

        var alerts = await harness.Finance.GetBudgetAlertsAsync();

        alerts.Should().Contain(x => x.BudgetName == "Marketing budget" && x.Severity == "high" && x.ActualSpend == 150m);
    }

    [Fact]
    public async Task Reconciliation_DetectsInvoicePaymentMismatch()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        var orderId = await harness.SeedOrderAsync(customerId, 300m, OrderStatusNames.Completed);
        await harness.SeedInvoiceAsync(customerId, orderId, 300m, amountPaid: 300m, outstanding: 0m, status: InvoiceStatusNames.Paid);
        await harness.SeedPaymentAsync(customerId, orderId, 100m, PaymentStatusNames.Completed);

        var recon = await harness.Finance.ReconcileAsync();

        recon.InvoicePaymentMismatches.Should().BeGreaterThan(0);
        recon.Issues.Should().Contain(x => x.Kind == "invoice-payment-mismatch");
        recon.BankSyncClassification.Should().Be("UNAVAILABLE");
    }

    [Fact]
    public async Task Reminder_RequiresApprovalFingerprint_AndBlocksExternalSend()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        var orderId = await harness.SeedOrderAsync(customerId, 200m, OrderStatusNames.Completed);
        var invoiceId = await harness.SeedInvoiceAsync(
            customerId, orderId, 200m, 0m, 200m, InvoiceStatusNames.Overdue, dueDate: DateTime.UtcNow.Date.AddDays(-10));

        var prepared = await harness.Finance.PreparePaymentReminderAsync(new PrepareReminderRequest
        {
            InvoiceId = invoiceId,
            IdempotencyKey = "rem-1"
        });

        prepared.Created.Should().BeTrue();
        prepared.RequiresApproval.Should().BeTrue();
        prepared.ApprovalFingerprint.Should().NotBeNullOrWhiteSpace();
        prepared.IntegrationClassification.Should().Be("BLOCKED");

        var bad = () => harness.Finance.SubmitPaymentReminderAsync(
            prepared.ReminderId, approved: true, approvalFingerprint: "wrong-fingerprint", idempotencyKey: "rem-1");
        await bad.Should().ThrowAsync<Exception>();

        var submitted = await harness.Finance.SubmitPaymentReminderAsync(
            prepared.ReminderId, approved: true, prepared.ApprovalFingerprint, "rem-1");

        submitted.Status.Should().Be("Failed");
        submitted.IntegrationClassification.Should().Be("BLOCKED");
        submitted.Summary.Should().ContainEquivalentOf("blocked");
        (await harness.Db.FinancePaymentReminders.SingleAsync(x => x.Id == prepared.ReminderId))
            .SentAt.Should().BeNull();
    }

    [Fact]
    public async Task Reminder_DuplicatePrevention_ByIdempotency()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        var orderId = await harness.SeedOrderAsync(customerId, 180m, OrderStatusNames.Completed);
        var invoiceId = await harness.SeedInvoiceAsync(
            customerId, orderId, 180m, 0m, 180m, InvoiceStatusNames.Overdue, dueDate: DateTime.UtcNow.Date.AddDays(-5));

        var request = new PrepareReminderRequest { InvoiceId = invoiceId, IdempotencyKey = "dup-rem" };
        var first = await harness.Finance.PreparePaymentReminderAsync(request);
        var second = await harness.Finance.PreparePaymentReminderAsync(request);

        first.Created.Should().BeTrue();
        second.DuplicatePrevented.Should().BeTrue();
        second.Created.Should().BeFalse();
        harness.Db.FinancePaymentReminders.Count(x => x.IdempotencyKey == "dup-rem").Should().Be(1);
    }

    [Fact]
    public async Task TenantIsolation_OtherTenantDataHidden()
    {
        var a = await Harness.OpenAsync();
        var customerA = await a.SeedCustomerAsync("Alpha");
        var orderA = await a.SeedOrderAsync(customerA, 777m, OrderStatusNames.Completed);
        await a.SeedInvoiceAsync(customerA, orderA, 777m, 0m, 777m, InvoiceStatusNames.Sent);

        var b = await Harness.OpenAsync();
        var customerB = await b.SeedCustomerAsync("Beta");
        var orderB = await b.SeedOrderAsync(customerB, 55m, OrderStatusNames.Completed);
        await b.SeedInvoiceAsync(customerB, orderB, 55m, 0m, 55m, InvoiceStatusNames.Sent);

        var arB = await b.Finance.GetReceivablesAsync();
        var intelB = await b.Finance.GetIntelligenceAsync(period: "month");

        arB.Items.Should().NotContain(x => x.OutstandingAmount == 777m);
        arB.TotalOutstanding.Should().Be(55m);
        intelB.SalesRevenue.Value.Should().Be(55m);
    }

    [Fact]
    public async Task Monitor_SafeRetry_WritesActivity()
    {
        var harness = await Harness.OpenAsync(seedOpeningBalance: true);
        var customerId = await harness.SeedCustomerAsync();
        var orderId = await harness.SeedOrderAsync(customerId, 90m, OrderStatusNames.Completed);
        await harness.SeedInvoiceAsync(
            customerId, orderId, 90m, 0m, 90m, InvoiceStatusNames.Overdue, dueDate: DateTime.UtcNow.Date.AddDays(-8));

        await harness.Finance.RunScheduledMonitorAsync();
        await harness.Finance.RunScheduledMonitorAsync();

        harness.Db.FinanceAgentActivities.Should().NotBeEmpty();
        (await harness.Finance.GetActivityAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task NavlaTools_ExecuteFinanceIntelligenceWithoutInventingTotals()
    {
        var harness = await Harness.OpenAsync(seedOpeningBalance: true);
        await harness.Service.ActivateAsync("finance");
        var customerId = await harness.SeedCustomerAsync();
        await harness.SeedOrderAsync(customerId, 125m, OrderStatusNames.Completed);

        var run = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "finance",
            Objective = "Summarize recorded finance metrics without inventing totals.",
            Tools = ["GetFinanceIntelligence", "GetFinancialSummary"],
            ArgumentsJson = "{}"
        });

        run.Status.Should().BeOneOf(NalvaStatuses.Completed, NalvaStatuses.AwaitingApproval);
        harness.Db.NalvaExecutions.Should().Contain(x => x.AgentKey == "finance");

        var intel = await harness.Finance.GetIntelligenceAsync(period: "month");
        intel.SalesRevenue.Value.Should().Be(125m);
        intel.GrossProfit.Available.Should().BeFalse();
        intel.GrossProfit.Value.Should().BeNull();
    }

    [Fact]
    public void FinanceAgent_CatalogIncludesNewTools()
    {
        NalvaAgentCatalog.TryGet("finance", out var agent).Should().BeTrue();
        agent!.Tools.Should().Contain(new[]
        {
            "GetFinanceIntelligence",
            "AnalyzeExpenses",
            "GetReceivablesAging",
            "GetPayablesSchedule",
            "ForecastCashFlow",
            "ReconcileFinancialRecords",
            "GetBudgetStatus",
            "PreparePaymentReminder",
            "SubmitPaymentReminder",
            "GenerateFinancialReport",
            "CalculateProfitability"
        });

        NalvaToolCatalog.TryGet("GetFinanceIntelligence", out _).Should().BeTrue();
        NalvaToolCatalog.TryGet("PreparePaymentReminder", out _).Should().BeTrue();
        NalvaToolCatalog.TryGet("SubmitPaymentReminder", out _).Should().BeTrue();
        NalvaWorkflowCatalog.TryGet("financial-monitoring", out _).Should().BeTrue();
        NalvaWorkflowCatalog.TryGet("overdue-payment-followup", out _).Should().BeTrue();
    }

    private sealed class Harness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required IFinanceAgentService Finance { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaExecutionEngine Engine { get; init; }
        public required Guid TenantId { get; init; }

        public static async Task<Harness> OpenAsync(bool seedOpeningBalance = false, decimal openingCash = 500m)
        {
            var (db, tenantId, provider) = TenantDatabase.Open();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Finance Test Co",
                Slug = $"finance-{tenantId:N}"[..20],
                Email = "finance@example.com",
                Phone = "555",
                Address = "1 Finance St",
                Country = "Pakistan",
                BusinessType = "retail",
                OwnerUserId = "user-1",
                SubscriptionPlanId = Guid.NewGuid(),
                Status = TenantStatus.Active,
                IsActive = true,
                Currency = "USD",
                Timezone = "UTC"
            });
            if (seedOpeningBalance)
            {
                db.TenantSettings.Add(new TenantSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    OpeningCashBalance = openingCash,
                    Currency = "USD",
                    Timezone = "UTC"
                });
            }

            await db.SaveChangesAsync();

            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            var audit = new EntityAuditService(db, user.Object);
            var finance = new FinanceAgentService(
                db, provider, user.Object, audit, NullLogger<FinanceAgentService>.Instance);

            var gateway = new NalvaToolGateway(
                db, provider, new CrmOperationsService(db, user.Object), finance: finance);
            var engine = new NalvaExecutionEngine(
                db, provider, user.Object, gateway, new SilentModel(), NullLogger<NalvaExecutionEngine>.Instance);
            var service = new NalvaWorkforceService(db, user.Object, engine, new NalvaWorkflowEngine(db, engine));

            return new Harness
            {
                Db = db,
                Finance = finance,
                Service = service,
                Engine = engine,
                TenantId = tenantId
            };
        }

        public async Task<Guid> SeedCustomerAsync(string firstName = "Pat")
        {
            var id = Guid.NewGuid();
            Db.Customers.Add(new Customer
            {
                Id = id,
                FirstName = firstName,
                LastName = "Customer",
                Email = $"{firstName.ToLowerInvariant()}@example.com",
                PhoneNumber = "555-0100",
                Address = "Street 1",
                City = "Lahore",
                Country = "Pakistan",
                PostalCode = "54000"
            });
            await Db.SaveChangesAsync();
            return id;
        }

        public async Task<Guid> SeedOrderAsync(
            Guid customerId,
            decimal grandTotal,
            string status,
            decimal unitCost = 0m,
            decimal quantity = 1m,
            DateTime? when = null)
        {
            var orderId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var unitPrice = quantity == 0 ? grandTotal : grandTotal / quantity;
            Db.Orders.Add(new Order
            {
                Id = orderId,
                CustomerId = customerId,
                OrderNumber = $"ORD-{orderId.ToString("N")[..8]}",
                OrderDate = when ?? DateTime.UtcNow,
                Status = status,
                GrandTotal = grandTotal,
                TotalAmount = grandTotal,
                RowVersion = Guid.NewGuid().ToByteArray()[..8]
            });
            Db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                UnitCost = unitCost,
                Total = grandTotal
            });
            await Db.SaveChangesAsync();
            return orderId;
        }

        public async Task<Guid> SeedInvoiceAsync(
            Guid customerId,
            Guid orderId,
            decimal grandTotal,
            decimal amountPaid,
            decimal outstanding,
            string status,
            DateTime? dueDate = null,
            DateTime? invoiceDate = null)
        {
            var id = Guid.NewGuid();
            Db.Invoices.Add(new Invoice
            {
                Id = id,
                InvoiceNumber = $"INV-{id.ToString("N")[..8]}",
                OrderId = orderId,
                CustomerId = customerId,
                InvoiceDate = invoiceDate ?? DateTime.UtcNow,
                DueDate = dueDate ?? DateTime.UtcNow.AddDays(14),
                Status = status,
                SubTotal = grandTotal,
                GrandTotal = grandTotal,
                AmountPaid = amountPaid,
                OutstandingAmount = outstanding
            });
            await Db.SaveChangesAsync();
            return id;
        }

        public async Task SeedPaymentAsync(
            Guid customerId,
            Guid orderId,
            decimal amount,
            string status,
            decimal refunded = 0m,
            DateTime? when = null)
        {
            Db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                CustomerId = customerId,
                Amount = amount,
                RefundedAmount = refunded,
                PaymentMethod = PaymentMethodNames.Cash,
                PaymentDate = when ?? DateTime.UtcNow,
                Status = status,
                Currency = "USD",
                RowVersion = Guid.NewGuid().ToByteArray()[..8]
            });
            await Db.SaveChangesAsync();
        }

        public async Task<Guid> SeedExpenseCategoryAsync(string name)
        {
            var id = Guid.NewGuid();
            Db.ExpenseCategories.Add(new ExpenseCategory
            {
                Id = id,
                Name = name,
                Description = name,
                IsActive = true
            });
            await Db.SaveChangesAsync();
            return id;
        }

        public async Task SeedExpenseAsync(
            Guid categoryId,
            string title,
            decimal amount,
            string status,
            DateTime? when = null,
            string? vendor = null)
        {
            Db.Expenses.Add(new Expense
            {
                Id = Guid.NewGuid(),
                Title = title,
                Amount = amount,
                ExpenseDate = when ?? DateTime.UtcNow,
                ExpenseCategoryId = categoryId,
                PaymentMethod = PaymentMethodNames.Cash,
                Vendor = vendor,
                Status = status
            });
            await Db.SaveChangesAsync();
        }

        public async Task<Guid> SeedSupplierAsync(string name)
        {
            var id = Guid.NewGuid();
            Db.Suppliers.Add(new Supplier
            {
                Id = id,
                Name = name,
                Email = $"{name.Replace(" ", "").ToLowerInvariant()}@example.com",
                Phone = "555",
                Address = "Addr"
            });
            await Db.SaveChangesAsync();
            return id;
        }

        public async Task SeedPurchaseAsync(Guid supplierId, decimal total, string status = PurchaseOrderStatusNames.Approved)
        {
            Db.Purchases.Add(new Purchase
            {
                Id = Guid.NewGuid(),
                SupplierId = supplierId,
                PurchaseDate = DateTime.UtcNow,
                Status = status,
                TotalAmount = total,
                ReferenceNumber = $"PO-{Guid.NewGuid().ToString("N")[..6]}"
            });
            await Db.SaveChangesAsync();
        }

        public async Task SeedPurchaseInvoiceAsync(Guid supplierId, string number, decimal total, decimal amountPaid)
        {
            Db.PurchaseInvoices.Add(new PurchaseInvoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = number,
                SupplierId = supplierId,
                IssueDate = DateTime.UtcNow.Date,
                DueDate = DateTime.UtcNow.Date.AddDays(10),
                SubTotal = total,
                Total = total,
                AmountPaid = amountPaid,
                Status = "Issued"
            });
            await Db.SaveChangesAsync();
        }
    }

    private sealed class SilentModel : ILlmChatClient
    {
        public bool IsConfigured => false;
        public Task<string?> GenerateReplyAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) => Task.FromResult<string?>(null);
        public async IAsyncEnumerable<string> StreamReplyAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) { await Task.CompletedTask; yield break; }
        public Task<(string? Reply, int? TokenUsage)> GenerateWithToolsAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, IReadOnlyList<object> toolResults, CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) => Task.FromResult<(string?, int?)>((null, null));
    }
}

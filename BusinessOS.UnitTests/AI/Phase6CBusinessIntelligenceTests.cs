using BusinessOS.Application.Common.Caching;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.BusinessIntelligence;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.BusinessIntelligence;
using BusinessOS.Infrastructure.AI.Nalva;
using BusinessOS.Infrastructure.AI.Nalva.Suppliers;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class Phase6CBusinessIntelligenceTests
{
    [Fact]
    public async Task SalesForecast_Unavailable_WithThinHistory()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        await harness.SeedOrderAsync(customerId, 100m, OrderStatusNames.Completed, when: DateTime.UtcNow);

        var forecast = await harness.Sales.ForecastSalesAsync(1);

        forecast.Available.Should().BeFalse();
        forecast.IsPrediction.Should().BeTrue();
        forecast.Confidence.Should().Be("unavailable");
        forecast.NextPeriodPredicted.Should().BeNull();
    }

    [Fact]
    public async Task SalesForecast_Available_WithEnoughMonthlyHistory()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-6);
        for (var i = 0; i < 6; i++)
            await harness.SeedOrderAsync(customerId, 100m + i * 20m, OrderStatusNames.Completed, when: start.AddMonths(i).AddDays(5));

        var forecast = await harness.Sales.ForecastSalesAsync(1);

        forecast.Available.Should().BeTrue();
        forecast.IsPrediction.Should().BeTrue();
        forecast.NextPeriodPredicted.Should().NotBeNull();
        forecast.Assumptions.Should().NotBeEmpty();
        forecast.DataClassification.Should().Be("REAL");
    }

    [Fact]
    public async Task ChurnRisk_MarksSilentCustomers_AsPrediction()
    {
        var harness = await Harness.OpenAsync();
        var active = await harness.SeedCustomerAsync("Active");
        var quiet = await harness.SeedCustomerAsync("Quiet");
        await harness.SeedOrderAsync(active, 50m, OrderStatusNames.Completed, when: DateTime.UtcNow.AddDays(-10));
        await harness.SeedOrderAsync(quiet, 80m, OrderStatusNames.Completed, when: DateTime.UtcNow.AddDays(-120));

        var risks = await harness.Customers.GetChurnRisksAsync();

        risks.Should().Contain(x => x.CustomerId == quiet);
        risks.Should().NotContain(x => x.CustomerId == active);
        risks.Single(x => x.CustomerId == quiet).IsPrediction.Should().BeTrue();
    }

    [Fact]
    public async Task ExecutiveBrief_DoesNotInventRevenueForecast_WhenUnavailable()
    {
        var harness = await Harness.OpenAsync();
        var brief = await harness.Executive.ExpectedRevenueNextMonthAsync();

        brief.ContainsPredictions.Should().BeTrue();
        brief.Answer.ToLowerInvariant().Should().Contain("unavailable");
        brief.DataClassification.Should().Be("REAL");
    }

    [Fact]
    public async Task HealthOverview_UsesCompletedOrderRevenue()
    {
        var harness = await Harness.OpenAsync();
        var customerId = await harness.SeedCustomerAsync();
        await harness.SeedOrderAsync(customerId, 250m, OrderStatusNames.Completed);
        await harness.SeedOrderAsync(customerId, 999m, OrderStatusNames.Pending);

        var health = await harness.Bi.GetHealthOverviewAsync(period: "month");

        health.SalesRevenue.Available.Should().BeTrue();
        health.SalesRevenue.Value.Should().Be(250m);
        health.SalesRevenue.IsPrediction.Should().BeFalse();
        health.DataClassification.Should().Be("REAL");
    }

    [Fact]
    public async Task TenantIsolation_PredictionLogs_DoNotLeak()
    {
        var a = await Harness.OpenAsync();
        var b = await Harness.OpenAsync();
        await a.Predictions.RecordForecastAsync(new PredictionRecordRequest
        {
            MetricKey = "sales-revenue-monthly",
            Method = "test",
            PredictedValue = 123m,
            Confidence = "low",
            TargetPeriodStart = DateTime.UtcNow.Date,
            TargetPeriodEnd = DateTime.UtcNow.Date.AddMonths(1)
        });

        var summaryB = await b.Predictions.GetAccuracySummaryAsync();
        summaryB.TotalRecorded.Should().Be(0);
    }

    private sealed class Harness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required ISalesIntelligenceService Sales { get; init; }
        public required ICustomerIntelligenceService Customers { get; init; }
        public required IBusinessIntelligenceService Bi { get; init; }
        public required IExecutiveIntelligenceService Executive { get; init; }
        public required IPredictionAccuracyService Predictions { get; init; }
        public required Guid TenantId { get; init; }

        public static async Task<Harness> OpenAsync()
        {
            var (db, tenantId, provider) = TenantDatabase.Open();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "BI Co",
                Slug = $"bi-{tenantId.ToString("N")[..8]}",
                Email = "bi@example.com",
                Phone = "+10000000000",
                Address = "1 BI St",
                Country = "Pakistan",
                BusinessType = "retail",
                OwnerUserId = "user-1",
                SubscriptionPlanId = Guid.NewGuid(),
                Status = TenantStatus.Active,
                IsActive = true,
                Currency = "USD",
                Timezone = "UTC"
            });
            await db.SaveChangesAsync();

            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            var cache = new Mock<ICacheService>();
            cache.Setup(x => x.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<ExecutiveDashboardDto>>>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, Func<CancellationToken, Task<ExecutiveDashboardDto>>, TimeSpan?, TimeSpan?, CancellationToken>(
                    (_, factory, _, _, ct) => factory(ct));

            var audit = new EntityAuditService(db, user.Object);
            var finance = new FinanceAgentService(db, provider, user.Object, audit, NullLogger<FinanceAgentService>.Instance);
            var inventory = TenantDatabase.Inventory(db);
            var procurement = new InventoryProcurementAgentService(
                db, provider, user.Object, inventory, new UnavailableSupplierResearchProvider(), audit,
                NullLogger<InventoryProcurementAgentService>.Instance);
            var predictions = new PredictionAccuracyService(db, provider, user.Object);
            var sales = new SalesIntelligenceService(db, user.Object, predictions);
            var customers = new CustomerIntelligenceService(db, user.Object);
            var bi = new BusinessIntelligenceService(
                db, provider, user.Object, cache.Object, sales, customers, finance, procurement, predictions);
            var executive = new ExecutiveIntelligenceService(bi, sales, customers, procurement, db, provider, user.Object);

            await Task.CompletedTask;
            return new Harness
            {
                Db = db,
                Sales = sales,
                Customers = customers,
                Bi = bi,
                Executive = executive,
                Predictions = predictions,
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
                Email = $"{firstName.ToLowerInvariant()}{id.ToString("N")[..6]}@example.com",
                PhoneNumber = "555-0100",
                Address = "Street 1",
                City = "Lahore",
                Country = "Pakistan",
                PostalCode = "54000"
            });
            await Db.SaveChangesAsync();
            return id;
        }

        public async Task SeedOrderAsync(Guid customerId, decimal grandTotal, string status, DateTime? when = null)
        {
            var orderId = Guid.NewGuid();
            var productId = Guid.NewGuid();
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
                Quantity = 1,
                UnitPrice = grandTotal,
                UnitCost = 0,
                Total = grandTotal
            });
            await Db.SaveChangesAsync();
        }
    }
}

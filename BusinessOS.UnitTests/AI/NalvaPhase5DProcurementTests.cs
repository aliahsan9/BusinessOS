using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.ProcurementAgent;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Nalva;
using BusinessOS.Infrastructure.AI.Nalva.Suppliers;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class NalvaPhase5DProcurementTests
{
    [Fact]
    public async Task InventoryRisks_DetectLowAndOutOfStock_WithoutInventingValues()
    {
        var harness = await Harness.OpenAsync();
        var low = await harness.SeedProductAsync("Widget Low", "WL-1", stock: 2, reorder: 10, max: 40);
        var outOfStock = await harness.SeedProductAsync("Widget Out", "WO-1", stock: 0, reorder: 5, max: 20);

        var risks = await harness.Procurement.GetInventoryRisksAsync();

        risks.Should().Contain(x => x.ProductId == low && x.RiskKind == "low-stock" && x.CurrentStock == 2);
        risks.Should().Contain(x => x.ProductId == outOfStock && x.RiskKind == "out-of-stock" && x.CurrentStock == 0);
        risks.Should().OnlyContain(x => x.Explanation.Length > 0);
    }

    [Fact]
    public async Task ReorderQuantity_AccountsForPendingInboundPurchases()
    {
        var harness = await Harness.OpenAsync();
        var productId = await harness.SeedProductAsync("Cable", "CB-1", stock: 1, reorder: 20, max: 50, cost: 5);
        var supplierId = await harness.SeedSupplierAsync("Acme");
        harness.Db.Purchases.Add(new Purchase
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            PurchaseDate = DateTime.UtcNow,
            Status = PurchaseOrderStatusNames.Approved,
            TotalAmount = 50,
            PurchaseItems =
            [
                new PurchaseItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = 10,
                    UnitPrice = 5,
                    Total = 50,
                    ReceivedQuantity = 0
                }
            ]
        });
        await harness.Db.SaveChangesAsync();

        var risks = await harness.Procurement.GetInventoryRisksAsync();
        var risk = risks.Single(x => x.ProductId == productId);
        risk.IncomingQuantity.Should().Be(10);
        risk.EffectiveAvailable.Should().Be(11);
        risk.SuggestedReorderQuantity.Should().BeLessThan(50);
    }

    [Fact]
    public async Task Demand_WithInsufficientHistory_IsUnavailable()
    {
        var harness = await Harness.OpenAsync();
        var productId = await harness.SeedProductAsync("Sparse", "SP-1", stock: 8, reorder: 2, max: 20);

        var demand = await harness.Procurement.AnalyzeDemandAsync(productId);
        demand.Should().ContainSingle();
        demand[0].ForecastAvailable.Should().BeFalse();
        demand[0].Confidence.Should().Be("unavailable");
    }

    [Fact]
    public async Task Demand_WithSufficientSales_ComputesCoverage()
    {
        var harness = await Harness.OpenAsync();
        var productId = await harness.SeedProductAsync("Fast", "FT-1", stock: 30, reorder: 10, max: 100);
        await harness.SeedSalesHistoryAsync(productId, days: 30, unitsPerDay: 2);

        var demand = await harness.Procurement.AnalyzeDemandAsync(productId);
        demand[0].ForecastAvailable.Should().BeTrue();
        demand[0].AverageDailyDemand.Should().BeApproximately(2m, 0.01m);
        demand[0].DaysOfCover.Should().BeApproximately(15m, 0.5m);
        demand[0].Confidence.Should().BeOneOf("medium", "high");
    }

    [Fact]
    public async Task SupplierComparison_LabelsResearchAsSimulated_AndHistoryAsReal()
    {
        var harness = await Harness.OpenAsync(useTestResearch: true);
        var productId = await harness.SeedProductAsync("Bolt", "BT-1", stock: 1, reorder: 10, max: 40, cost: 3);
        var supplierId = await harness.SeedSupplierAsync("Local Metals");
        harness.Db.Purchases.Add(new Purchase
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            PurchaseDate = DateTime.UtcNow.AddDays(-10),
            Status = PurchaseOrderStatusNames.Received,
            TotalAmount = 30,
            PurchaseItems =
            [
                new PurchaseItem { Id = Guid.NewGuid(), ProductId = productId, Quantity = 10, UnitPrice = 3, Total = 30 }
            ]
        });
        await harness.Db.SaveChangesAsync();

        var options = await harness.Procurement.CompareSuppliersAsync(productId, 20);
        options.Should().Contain(x => x.Classification == "REAL" && x.SupplierId == supplierId);
        options.Should().Contain(x => x.Classification == "SIMULATED" && x.SupplierId == null);
        options.Should().NotContain(x => x.Classification == "SIMULATED" && x.Source == "stored-purchase-history");
    }

    [Fact]
    public async Task PrepareAndApprove_CreatesPendingPurchase_WithoutExternalSend()
    {
        var harness = await Harness.OpenAsync();
        var productId = await harness.SeedProductAsync("Ink", "INK-1", stock: 0, reorder: 5, max: 25, cost: 4);
        var supplierId = await harness.SeedSupplierAsync("Office Co");

        var prepared = await harness.Procurement.PreparePurchaseOrderAsync(new PreparePurchaseOrderRequest
        {
            ProductId = productId,
            SupplierId = supplierId,
            Quantity = 20,
            UnitPrice = 4,
            IdempotencyKey = "po-ink-1",
            Reason = "Out of stock"
        });

        prepared.Created.Should().BeTrue();
        prepared.RequiresApproval.Should().BeTrue();
        prepared.PurchaseOrderId.Should().NotBeNull();
        var draft = await harness.Db.Purchases.SingleAsync(x => x.Id == prepared.PurchaseOrderId);
        draft.Status.Should().Be(PurchaseOrderStatusNames.Draft);
        draft.SentToSupplier.Should().BeFalse();

        var submitted = await harness.Procurement.SubmitPreparedPurchaseAsync(
            prepared.RecommendationId, "po-ink-1", approved: true, prepared.ApprovalFingerprint);

        submitted.Created.Should().BeTrue();
        var pending = await harness.Db.Purchases.SingleAsync(x => x.Id == prepared.PurchaseOrderId);
        pending.Status.Should().Be(PurchaseOrderStatusNames.Pending);
        pending.SentToSupplier.Should().BeFalse();
    }

    [Fact]
    public async Task MaterialChange_InvalidatesApprovalFingerprint()
    {
        var harness = await Harness.OpenAsync();
        var productId = await harness.SeedProductAsync("Paper", "PP-1", stock: 1, reorder: 10, max: 50, cost: 2);
        var supplierId = await harness.SeedSupplierAsync("Paper Inc");
        var prepared = await harness.Procurement.PreparePurchaseOrderAsync(new PreparePurchaseOrderRequest
        {
            ProductId = productId,
            SupplierId = supplierId,
            Quantity = 10,
            UnitPrice = 2,
            IdempotencyKey = "paper-1"
        });

        var purchase = await harness.Db.Purchases.Include(x => x.PurchaseItems).SingleAsync(x => x.Id == prepared.PurchaseOrderId);
        purchase.PurchaseItems.First().UnitPrice = 9;
        purchase.PurchaseItems.First().Total = 90;
        purchase.TotalAmount = 90;
        await harness.Db.SaveChangesAsync();

        var act = () => harness.Procurement.SubmitPreparedPurchaseAsync(
            prepared.RecommendationId, "paper-1", true, prepared.ApprovalFingerprint);

        await act.Should().ThrowAsync<Exception>();
        (await harness.Db.ProcurementRecommendations.SingleAsync(x => x.Id == prepared.RecommendationId))
            .Status.Should().Be("Invalidated");
    }

    [Fact]
    public async Task DuplicateIdempotency_PreventsSecondPurchase()
    {
        var harness = await Harness.OpenAsync();
        var productId = await harness.SeedProductAsync("Tape", "TP-1", stock: 0, reorder: 5, max: 20, cost: 1);
        var supplierId = await harness.SeedSupplierAsync("Tape Co");
        var request = new PreparePurchaseOrderRequest
        {
            ProductId = productId,
            SupplierId = supplierId,
            Quantity = 5,
            UnitPrice = 1,
            IdempotencyKey = "tape-dup"
        };

        var first = await harness.Procurement.PreparePurchaseOrderAsync(request);
        var second = await harness.Procurement.PreparePurchaseOrderAsync(request);

        first.Created.Should().BeTrue();
        second.DuplicatePrevented.Should().BeTrue();
        harness.Db.Purchases.Count(x => x.IdempotencyKey == "tape-dup").Should().Be(1);
    }

    [Fact]
    public async Task PartialReceive_UpdatesStockOnlyForVerifiedQuantity()
    {
        var harness = await Harness.OpenAsync();
        var productId = await harness.SeedProductAsync("Glue", "GL-1", stock: 0, reorder: 5, max: 30, cost: 2);
        var supplierId = await harness.SeedSupplierAsync("Glue Co");
        var itemId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        harness.Db.Purchases.Add(new Purchase
        {
            Id = purchaseId,
            SupplierId = supplierId,
            PurchaseDate = DateTime.UtcNow,
            Status = PurchaseOrderStatusNames.Approved,
            TotalAmount = 20,
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-1),
            PurchaseItems =
            [
                new PurchaseItem
                {
                    Id = itemId,
                    ProductId = productId,
                    Quantity = 10,
                    UnitPrice = 2,
                    Total = 20,
                    ReceivedQuantity = 0
                }
            ]
        });
        await harness.Db.SaveChangesAsync();

        var result = await harness.Procurement.ReceiveVerifiedAsync(new ReceiveVerificationRequest
        {
            PurchaseOrderId = purchaseId,
            Lines =
            [
                new ReceiveVerificationLineRequest { PurchaseItemId = itemId, ReceivedQuantity = 4, DamagedQuantity = 1 }
            ]
        });

        result.Status.Should().Be(PurchaseOrderStatusNames.PartiallyReceived);
        result.StockIncreasedBy.Should().Be(4);
        (await harness.Db.Inventories.SingleAsync(x => x.ProductId == productId)).CurrentStock.Should().Be(4);
        (await harness.Db.PurchaseItems.SingleAsync(x => x.Id == itemId)).DamagedQuantity.Should().Be(1);
        (await harness.Db.PurchaseItems.SingleAsync(x => x.Id == itemId)).ReceivedQuantity.Should().Be(4);
    }

    [Fact]
    public async Task DelayedDeliveries_AreDetectedFromExpectedDate()
    {
        var harness = await Harness.OpenAsync();
        var productId = await harness.SeedProductAsync("Box", "BX-1", stock: 1, reorder: 5, max: 20, cost: 1);
        var supplierId = await harness.SeedSupplierAsync("Box Co");
        harness.Db.Purchases.Add(new Purchase
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            PurchaseDate = DateTime.UtcNow.AddDays(-10),
            Status = PurchaseOrderStatusNames.Approved,
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-3),
            TotalAmount = 5,
            ReferenceNumber = "DELAY-1",
            PurchaseItems =
            [
                new PurchaseItem { Id = Guid.NewGuid(), ProductId = productId, Quantity = 5, UnitPrice = 1, Total = 5 }
            ]
        });
        await harness.Db.SaveChangesAsync();

        var deliveries = await harness.Procurement.GetDeliveriesAsync();
        deliveries.Should().Contain(x => x.IsDelayed && x.ReferenceNumber == "DELAY-1" && x.DaysLate >= 3);
    }

    [Fact]
    public async Task TenantIsolation_DoesNotLeakOtherCompanyInventory()
    {
        var a = await Harness.OpenAsync();
        var productA = await a.SeedProductAsync("Secret A", "SA-1", stock: 1, reorder: 10, max: 20);
        var b = await Harness.OpenAsync();
        await b.SeedProductAsync("Secret B", "SB-1", stock: 0, reorder: 5, max: 10);

        var risksB = await b.Procurement.GetInventoryRisksAsync();
        risksB.Should().NotContain(x => x.ProductId == productA);
        risksB.Should().Contain(x => x.Sku == "SB-1");
    }

    [Fact]
    public async Task BackgroundMonitor_RetriesSafely_WithoutDuplicateDelayNoticesSameDay()
    {
        var harness = await Harness.OpenAsync();
        var productId = await harness.SeedProductAsync("Lag", "LG-1", stock: 0, reorder: 5, max: 10, cost: 1);
        var supplierId = await harness.SeedSupplierAsync("Lag Co");
        harness.Db.Purchases.Add(new Purchase
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            PurchaseDate = DateTime.UtcNow.AddDays(-5),
            Status = PurchaseOrderStatusNames.Approved,
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(-2),
            TotalAmount = 5,
            PurchaseItems =
            [
                new PurchaseItem { Id = Guid.NewGuid(), ProductId = productId, Quantity = 5, UnitPrice = 1, Total = 5 }
            ]
        });
        await harness.Db.SaveChangesAsync();

        await harness.Procurement.RunScheduledMonitorAsync();
        await harness.Procurement.RunScheduledMonitorAsync();

        harness.Db.NalvaWorkItems.Count(x => x.Kind == "DeliveryDelay").Should().Be(1);
        harness.Db.NalvaWorkItems.Count(x => x.Kind == "InventoryAlert").Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NavlaProcurementTools_ExecuteAgainstTenantData()
    {
        var harness = await Harness.OpenAsync();
        await harness.Service.ActivateAsync("procurement");
        var productId = await harness.SeedProductAsync("Navla SKU", "NV-1", stock: 1, reorder: 8, max: 30, cost: 2);
        var supplierId = await harness.SeedSupplierAsync("Navla Supplier");

        var run = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "procurement",
            Objective = "Check which products need restocking and prepare a purchase for approval.",
            Tools = ["GetInventoryRisks", "PreparePurchaseOrder"],
            ArgumentsJson = $"{{\"productId\":\"{productId}\",\"supplierId\":\"{supplierId}\",\"quantity\":12,\"unitPrice\":2,\"idempotencyKey\":\"navla-po-1\"}}"
        });

        run.Status.Should().BeOneOf(NalvaStatuses.Completed, NalvaStatuses.AwaitingApproval);
        harness.Db.Purchases.Should().Contain(x => x.IdempotencyKey == "navla-po-1");
        harness.Db.NalvaExecutions.Should().Contain(x => x.AgentKey == "procurement");
    }

    [Fact]
    public void Fingerprint_ChangesWhenQuantityChanges()
    {
        var supplier = Guid.NewGuid();
        var product = Guid.NewGuid();
        var f1 = InventoryProcurementAgentService.BuildFingerprint(supplier, product, 10, 2, null, null);
        var f2 = InventoryProcurementAgentService.BuildFingerprint(supplier, product, 11, 2, null, null);
        f1.Should().NotBe(f2);
    }

    [Fact]
    public void ProcurementWorkflows_AreRegistered()
    {
        NalvaWorkflowCatalog.TryGet("inventory-automation", out var inventory).Should().BeTrue();
        inventory!.Steps.SelectMany(x => x.Tools).Should().Contain("PreparePurchaseOrder");
        NalvaWorkflowCatalog.TryGet("procurement-daily-monitor", out _).Should().BeTrue();
        NalvaWorkflowCatalog.TryGet("delivery-followup", out var delivery).Should().BeTrue();
        delivery!.TriggerEvent.Should().Be(NalvaEventNames.DeliveryDelayed);
    }

    private sealed class Harness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required IInventoryProcurementAgentService Procurement { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaExecutionEngine Engine { get; init; }
        public required Guid TenantId { get; init; }

        public static async Task<Harness> OpenAsync(bool useTestResearch = false)
        {
            var (db, tenantId, provider) = TenantDatabase.Open();
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            ISupplierResearchProvider research = useTestResearch
                ? new TestSupplierResearchProvider()
                : new UnavailableSupplierResearchProvider();

            var inventory = TenantDatabase.Inventory(db);
            var audit = new EntityAuditService(db, user.Object);
            var procurement = new InventoryProcurementAgentService(
                db, provider, user.Object, inventory, research, audit,
                NullLogger<InventoryProcurementAgentService>.Instance);

            var gateway = new NalvaToolGateway(db, provider, new CrmOperationsService(db, user.Object), procurement: procurement);
            var engine = new NalvaExecutionEngine(db, provider, user.Object, gateway, new SilentModel(), NullLogger<NalvaExecutionEngine>.Instance);
            var service = new NalvaWorkforceService(db, user.Object, engine, new NalvaWorkflowEngine(db, engine));

            await Task.CompletedTask;
            return new Harness
            {
                Db = db,
                Procurement = procurement,
                Service = service,
                Engine = engine,
                TenantId = tenantId
            };
        }

        public async Task<Guid> SeedProductAsync(string name, string sku, decimal stock, decimal reorder, decimal max, decimal cost = 1m)
        {
            var categoryId = Guid.NewGuid();
            Db.Categories.Add(new Category { Id = categoryId, Name = $"Cat-{sku}", Description = "t" });
            var productId = Guid.NewGuid();
            Db.Products.Add(new Product
            {
                Id = productId,
                CategoryId = categoryId,
                Name = name,
                SKU = sku,
                CostPrice = cost,
                SalePrice = cost * 2,
                CurrentStock = stock,
                ReorderLevel = reorder,
                IsActive = true,
                RowVersion = Guid.NewGuid().ToByteArray()[..8]
            });
            Db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                CurrentStock = stock,
                ReorderLevel = reorder,
                MinimumStockLevel = reorder,
                MaximumStockLevel = max,
                LastUpdated = DateTime.UtcNow,
                RowVersion = Guid.NewGuid().ToByteArray()[..8]
            });
            await Db.SaveChangesAsync();
            return productId;
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

        public async Task SeedSalesHistoryAsync(Guid productId, int days, decimal unitsPerDay)
        {
            var customerId = Guid.NewGuid();
            Db.Customers.Add(new Customer
            {
                Id = customerId,
                FirstName = "Buyer",
                LastName = "One",
                Email = "buyer@example.com",
                PhoneNumber = "1",
                Address = "x",
                City = "Lahore",
                Country = "Pakistan",
                PostalCode = "54000"
            });
            for (var i = 0; i < days; i++)
            {
                var orderId = Guid.NewGuid();
                Db.Orders.Add(new Order
                {
                    Id = orderId,
                    CustomerId = customerId,
                    OrderNumber = $"ORD-{i}",
                    OrderDate = DateTime.UtcNow.Date.AddDays(-i),
                    Status = OrderStatusNames.Completed,
                    GrandTotal = unitsPerDay * 2,
                    TotalAmount = unitsPerDay * 2,
                    RowVersion = Guid.NewGuid().ToByteArray()[..8]
                });
                Db.OrderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = productId,
                    Quantity = unitsPerDay,
                    UnitPrice = 2,
                    Total = unitsPerDay * 2
                });
            }
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

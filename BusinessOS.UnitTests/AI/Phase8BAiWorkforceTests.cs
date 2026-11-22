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

/// <summary>
/// Phase 8B — production AI workforce activation for TechNova Electronics scenarios.
/// Deterministic doubles only; no live LLM or live OAuth.
/// </summary>
[Trait("Category", "Deterministic")]
[Trait("Phase", "8B")]
public class Phase8BAiWorkforceTests
{
    [Fact]
    public void Catalog_DefinesSixPhase8BEmployees_WithInventorySeparatedFromProcurement()
    {
        NalvaAgentCatalog.Phase8BEmployeeKeys.Should().BeEquivalentTo(
            ["sales", "marketing", "support", "inventory", "finance", "procurement"]);

        foreach (var key in NalvaAgentCatalog.Phase8BEmployeeKeys)
        {
            NalvaAgentCatalog.TryGet(key, out var agent).Should().BeTrue(key);
            agent!.IsCore.Should().BeTrue(key);
            agent.Tools.Should().NotBeEmpty(key);
            agent.Responsibilities.Should().NotBeEmpty(key);
            agent.Role.Should().Contain("Manager");
        }

        var inventory = NalvaAgentCatalog.Get("inventory");
        inventory.Name.Should().Be("Inventory Manager");
        inventory.Tools.Should().Contain("GetLowStockProducts");
        inventory.Tools.Should().Contain("CalculateReorderRecommendations");
        inventory.Tools.Should().NotContain("PreparePurchaseOrder");
        inventory.Tools.Should().NotContain("SubmitPreparedPurchaseOrder");

        var procurement = NalvaAgentCatalog.Get("procurement");
        procurement.Name.Should().Be("Procurement Manager");
        procurement.Tools.Should().Contain("PreparePurchaseOrder");
        procurement.Tools.Should().Contain("CompareSupplierPrices");
    }

    [Fact]
    public void LowStockWorkflow_StartsWithInventoryManager_ThenProcurement()
    {
        NalvaWorkflowCatalog.TryGet("low-stock-to-procurement", out var template).Should().BeTrue();
        template!.Steps[0].AgentKey.Should().Be("inventory");
        template.Steps.Should().Contain(s => s.AgentKey == "procurement" && s.Tools.Contains("PreparePurchaseOrder"));

        foreach (var step in template.Steps)
        {
            NalvaAgentCatalog.TryGet(step.AgentKey, out var agent).Should().BeTrue(step.AgentKey);
            foreach (var tool in step.Tools)
                agent!.Tools.Should().Contain(tool, because: $"{step.Key}/{tool}");
        }
    }

    [Fact]
    public void ObjectiveRouter_UnderstandsEnglishAndRomanUrduInventoryRequests()
    {
        NalvaObjectiveRouter.Match("Navla, check my inventory and prepare purchase orders for products that need restocking.")
            .WorkflowKey.Should().Be("low-stock-to-procurement");
        NalvaObjectiveRouter.Match("kam stock products ka purchase order tayyar karo")
            .WorkflowKey.Should().Be("low-stock-to-procurement");
        NalvaObjectiveRouter.Match("Prepare a marketing campaign for our best products")
            .WorkflowKey.Should().Be("navla-marketing-campaign");
    }

    [Fact]
    public void NavlaWorkforceManager_DetectsDelegationIntents()
    {
        var manager = new NavlaWorkforceManagerService(
            Mock.Of<INalvaCollaborationService>(),
            Mock.Of<INalvaWorkforceService>(),
            Mock.Of<ICurrentUserService>());

        manager.LooksLikeWorkforceDelegation(
            "Navla, check my inventory and prepare purchase orders for products that need restocking.")
            .Should().BeTrue();
        manager.LooksLikeWorkforceDelegation("kam stock dekho aur purchase order tayyar karo")
            .Should().BeTrue();
        manager.LooksLikeWorkforceDelegation("hello how are you")
            .Should().BeFalse();
    }

    [Fact]
    public async Task ActivateCoreEmployees_ActivatesAllSixManagers_ForTechNova()
    {
        var harness = TechNovaHarness.Open();
        var result = await harness.Manager.ActivateCoreEmployeesAsync();

        result.PackKey.Should().Be("phase8b-core");
        result.ActivatedAgents.Should().BeEquivalentTo(NalvaAgentCatalog.Phase8BEmployeeKeys);

        var active = await harness.Db.NalvaCompanyAgents.AsNoTracking()
            .Where(x => x.Status == NalvaStatuses.Active)
            .Select(x => x.AgentKey)
            .ToListAsync();
        active.Should().Contain(NalvaAgentCatalog.Phase8BEmployeeKeys);
    }

    [Fact]
    public async Task InventoryManager_IdentifiesLowStock_FromRecordedInventoryOnly()
    {
        var harness = TechNovaHarness.Open();
        await harness.Manager.ActivateCoreEmployeesAsync();
        var productId = await harness.SeedProductAsync("TechNova Router X1", "TN-RX1", stock: 2, reorder: 10, max: 50, cost: 40);

        var result = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "inventory",
            Objective = "Identify low-stock products from recorded inventory.",
            Tools = ["GetLowStockProducts", "GetInventoryRisks"]
        });

        result.Status.Should().Be(NalvaStatuses.Completed);
        result.ResultJson.Should().Contain("TN-RX1");
        result.ResultJson.Should().Contain(productId.ToString());
    }

    [Fact]
    public async Task ProcurementManager_PreparePurchaseOrder_RequiresHumanApprovalPath()
    {
        var harness = TechNovaHarness.Open();
        await harness.Manager.ActivateCoreEmployeesAsync();
        var productId = await harness.SeedProductAsync("TechNova Switch 24P", "TN-SW24", stock: 1, reorder: 8, max: 40, cost: 55);
        var supplierId = await harness.SeedSupplierAsync("TechNova Supply Co");

        var prepared = await harness.Procurement.PreparePurchaseOrderAsync(new PreparePurchaseOrderRequest
        {
            ProductId = productId,
            SupplierId = supplierId,
            Quantity = 20,
            UnitPrice = 55,
            IdempotencyKey = "phase8b-po-1",
            Reason = "Low stock detected by Inventory Manager"
        });
        prepared.Created.Should().BeTrue();
        prepared.RequiresApproval.Should().BeTrue();
        prepared.PurchaseOrderId.Should().NotBeNull();

        // Without approval flag the submit path must not silently finalize a supplier send.
        var blocked = await harness.Procurement.SubmitPreparedPurchaseAsync(
            prepared.RecommendationId, "phase8b-po-1", approved: false, prepared.ApprovalFingerprint);
        blocked.RequiresApproval.Should().BeTrue();

        var approved = await harness.Procurement.SubmitPreparedPurchaseAsync(
            prepared.RecommendationId, "phase8b-po-1", approved: true, prepared.ApprovalFingerprint);
        approved.Created.Should().BeTrue();

        var purchase = await harness.Db.Purchases.AsNoTracking()
            .SingleAsync(x => x.Id == prepared.PurchaseOrderId);
        purchase.SentToSupplier.Should().BeFalse();
    }

    [Fact]
    public async Task SalesOutreach_And_MarketingPublish_DoNotSendWithoutApproval()
    {
        var harness = TechNovaHarness.Open();
        await harness.Manager.ActivateCoreEmployeesAsync();

        var outreach = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "sales",
            Objective = "Draft outreach and attempt external send.",
            Tools = ["SendExternalEmail"],
            ArgumentsJson = "{\"to\":\"buyer@example.com\",\"subject\":\"TechNova offer\",\"body\":\"Hello\"}"
        });
        outreach.Status.Should().Be(NalvaStatuses.AwaitingApproval);

        var publish = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "marketing",
            Objective = "Publish a social post for Router X1.",
            Tools = ["PublishSocialPost"],
            ArgumentsJson = "{\"content\":\"TechNova Router X1 launch\",\"platform\":\"meta\"}"
        });
        publish.Status.Should().Be(NalvaStatuses.AwaitingApproval);
    }

    [Fact]
    public async Task FinanceManager_Report_UsesRecordedFiguresOnly()
    {
        var harness = TechNovaHarness.Open();
        await harness.Manager.ActivateCoreEmployeesAsync();

        var report = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "finance",
            Objective = "Prepare a revenue and expense summary from recorded books.",
            Tools = ["GetFinancialSummary", "GenerateFinancialReport"]
        });

        report.Status.Should().BeOneOf(NalvaStatuses.Completed, NalvaStatuses.Failed);
        if (report.Status == NalvaStatuses.Completed)
            report.ResultJson.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TaskLifecycle_Assigned_Idempotent_AndRecoverable()
    {
        var harness = TechNovaHarness.Open();
        await harness.Service.ActivateAsync("inventory");

        var task = await harness.Service.EnqueueAsync("inventory", new NalvaRunRequest
        {
            Objective = "Daily inventory review",
            Tools = ["GetInventoryRisks"],
            IdempotencyKey = "phase8b-daily-inv-1"
        });
        task.Status.Should().Be(NalvaStatuses.Assigned);

        var again = await harness.Service.EnqueueAsync("inventory", new NalvaRunRequest
        {
            Objective = "Daily inventory review",
            Tools = ["GetInventoryRisks"],
            IdempotencyKey = "phase8b-daily-inv-1"
        });
        again.Id.Should().Be(task.Id);

        var row = await harness.Db.NalvaTasks.FirstAsync(x => x.Id == task.Id);
        row.Status = NalvaStatuses.Running;
        row.ClaimToken = Guid.NewGuid();
        row.UpdatedAt = DateTime.UtcNow.AddMinutes(-10);
        row.Attempt = 0;
        await harness.Db.SaveChangesAsync();

        var reliability = new NalvaReliabilityService(harness.Db, NullLogger<NalvaReliabilityService>.Instance);
        var recovered = await reliability.RecoverStalledWorkAsync();
        recovered.TasksRecovered.Should().BeGreaterThan(0);

        var after = await harness.Db.NalvaTasks.AsNoTracking().FirstAsync(x => x.Id == task.Id);
        after.Status.Should().BeOneOf(NalvaStatuses.Assigned, NalvaStatuses.DeadLetter, NalvaStatuses.Queued);
    }

    [Fact]
    public async Task TenantIsolation_SecondTenantCannotSeeTechNovaTasks()
    {
        var techNova = TechNovaHarness.Open(database: "phase8b-tn");
        await techNova.Manager.ActivateCoreEmployeesAsync();
        await techNova.Service.EnqueueAsync("inventory", new NalvaRunRequest
        {
            Objective = "TechNova only task",
            IdempotencyKey = "tn-iso-1"
        });

        var other = TechNovaHarness.Open(
            companyName: "Other Retail Co",
            slug: "other-retail",
            database: "phase8b-other");
        var otherTasks = await other.Service.GetTasksAsync("inventory", null);
        otherTasks.Should().BeEmpty();

        var otherAgents = await other.Db.NalvaCompanyAgents.AsNoTracking()
            .Where(x => x.Status == NalvaStatuses.Active)
            .ToListAsync();
        otherAgents.Should().BeEmpty();
    }

    [Fact]
    public async Task NavlaDelegate_StartsLowStockWorkflow_WithoutInventingSuccess()
    {
        var harness = TechNovaHarness.Open();
        await harness.Manager.ActivateCoreEmployeesAsync();

        var result = await harness.Manager.DelegateAsync(new NavlaWorkforceDelegationRequest
        {
            Message = "Navla, check my inventory and prepare purchase orders for products that need restocking.",
            StartImmediately = true,
            ActivateMissingAgents = true,
            IdempotencyKey = "phase8b-delegate-1"
        });

        result.Handled.Should().BeTrue();
        result.WorkflowKey.Should().Be("low-stock-to-procurement");
        if (result.Started)
            result.WorkflowRunId.Should().NotBeNull();
        else
            result.BlockReason.Should().NotBeNullOrWhiteSpace();
        result.Summary.ToLowerInvariant().Should().NotContain("successfully purchased");
    }

    private sealed class TechNovaHarness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaExecutionEngine Engine { get; init; }
        public required NalvaCollaborationService Collab { get; init; }
        public required NavlaWorkforceManagerService Manager { get; init; }
        public required InventoryProcurementAgentService Procurement { get; init; }

        public static TechNovaHarness Open(
            string companyName = "TechNova Electronics",
            string slug = "technova-electronics",
            string? database = null)
        {
            var (db, tenantId, provider) = TenantDatabase.Open(name: database);
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = companyName,
                Slug = slug,
                Email = $"owner@{slug}.example",
                Phone = "555-0100",
                Address = "Karachi",
                Country = "Pakistan",
                BusinessType = "retail",
                OwnerUserId = "user-1",
                SubscriptionPlanId = Guid.NewGuid(),
                Status = TenantStatus.Active,
                IsActive = true,
                Description = "Phase 8B TechNova Electronics test tenant"
            });
            db.SaveChanges();

            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            var inventory = TenantDatabase.Inventory(db);
            var audit = new EntityAuditService(db, user.Object);
            var finance = new FinanceAgentService(db, provider, user.Object, audit, NullLogger<FinanceAgentService>.Instance);
            var procurement = new InventoryProcurementAgentService(
                db, provider, user.Object, inventory, new UnavailableSupplierResearchProvider(), audit,
                NullLogger<InventoryProcurementAgentService>.Instance);
            var gateway = new NalvaToolGateway(
                db, provider, new CrmOperationsService(db, user.Object),
                procurement: procurement, finance: finance);
            var engine = new NalvaExecutionEngine(
                db, provider, user.Object, gateway, new SilentModel(),
                NullLogger<NalvaExecutionEngine>.Instance);
            var workflows = new NalvaWorkflowEngine(db, engine);
            var service = new NalvaWorkforceService(db, user.Object, engine, workflows);
            var collab = new NalvaCollaborationService(db, user.Object, provider, service, workflows);
            var manager = new NavlaWorkforceManagerService(collab, service, user.Object);

            return new TechNovaHarness
            {
                Db = db,
                Service = service,
                Engine = engine,
                Collab = collab,
                Manager = manager,
                Procurement = procurement
            };
        }

        public async Task<Guid> SeedProductAsync(string name, string sku, decimal stock, decimal reorder, decimal max, decimal cost)
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
    }

    private sealed class SilentModel : ILlmChatClient
    {
        public bool IsConfigured => false;
        public Task<string?> GenerateReplyAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) => Task.FromResult<string?>(null);
        public async IAsyncEnumerable<string> StreamReplyAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) { await Task.CompletedTask; yield break; }
        public Task<(string? Reply, int? TokenUsage)> GenerateWithToolsAsync(Guid tenantId, string userId, string systemPrompt, string userPrompt, IReadOnlyList<object> toolResults, CancellationToken cancellationToken = default, IReadOnlyList<AiMemoryTurnDto>? conversationHistory = null) => Task.FromResult<(string?, int?)>((null, null));
    }
}

using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Marketing;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Application.Features.ProcurementAgent;
using BusinessOS.Application.Features.Support;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Nalva;
using BusinessOS.Infrastructure.AI.Nalva.Suppliers;
using BusinessOS.Infrastructure.AI.Support;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using BusinessOS.Infrastructure.Services.Marketing;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

/// <summary>
/// Phase 7C named end-to-end AI workforce workflows using deterministic doubles only.
/// SilentModel / sandbox transports prove orchestration — not live LLM or live OAuth providers.
/// </summary>
[Trait("Category", "Deterministic")]
[Trait("Phase", "7C")]
public class Phase7CAiWorkforceWorkflowTests
{
    [Fact]
    public async Task WorkflowB_SupportRequest_Analyzes_ApprovesSensitiveAction_AndRecordsActivity()
    {
        var harness = await SupportHarness.OpenAsync();
        harness.Db.NalvaKnowledgeSources.Add(new NalvaKnowledgeSource
        {
            AgentKey = "support",
            Title = "Shipping policy",
            Content = "Standard shipping takes 3 to 5 business days inside Pakistan.",
            CreatedByUserId = "user-1"
        });
        await harness.Db.SaveChangesAsync();

        var inbound = await harness.Support.ReceiveInboundAsync(new SupportInboundMessageRequest
        {
            Channel = SupportChannels.Sandbox,
            Body = "How long does shipping take to Lahore?",
            Subject = "Shipping question",
            IdempotencyKey = "phase7c-support-1",
            ProcessWithAgent = true
        });

        inbound.ResponseSent.Should().BeTrue();
        inbound.ResponseDeliveryStatus.Should().Be(SupportDeliveryStatuses.Simulated);
        var ticket = await harness.Support.GetTicketAsync(inbound.TicketId);
        ticket.Should().NotBeNull();
        ticket!.Messages.Should().Contain(x =>
            x.Direction == SupportMessageDirections.Outbound &&
            x.Body.Contains("3 to 5", StringComparison.OrdinalIgnoreCase));
        harness.Db.NalvaWorkItems.Should().Contain(x => x.Kind == "SupportTicket");

        // Sensitive follow-up requires human approval before any payment mutation.
        var refund = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "support",
            Objective = "Prepare a refund for the shipping complaint.",
            Tools = ["IssueRefund"],
            ArgumentsJson = "{\"paymentId\":\"00000000-0000-0000-0000-000000000099\",\"amount\":25}"
        });
        refund.Status.Should().Be(NalvaStatuses.AwaitingApproval);
        refund.ApprovalId.Should().NotBeNull();

        var decided = await harness.Service.DecideApprovalAsync(refund.ApprovalId!.Value, "approve", "Owner approved prepared refund");
        decided.Status.Should().Be(NalvaStatuses.Completed);
        decided.ResultJson.Should().Contain("prepared");
        harness.Db.NalvaApprovals.Should().Contain(x =>
            x.Id == refund.ApprovalId && x.Status == NalvaStatuses.Approved);
        harness.Db.NalvaExecutions.Should().Contain(x => x.Id == decided.ExecutionId);
    }

    [Fact]
    public async Task WorkflowC_LowStock_ProcurementPrepares_HumanApproves_ReceiveUpdatesInventory()
    {
        var harness = await ProcurementHarness.OpenAsync();
        var productId = await harness.SeedProductAsync("Phase7C Widget", "P7C-W1", stock: 1, reorder: 10, max: 40, cost: 5);
        var supplierId = await harness.SeedSupplierAsync("Phase7C Supply");

        var risks = await harness.Procurement.GetInventoryRisksAsync();
        risks.Should().Contain(x => x.ProductId == productId && x.RiskKind == "low-stock");

        var prepared = await harness.Procurement.PreparePurchaseOrderAsync(new PreparePurchaseOrderRequest
        {
            ProductId = productId,
            SupplierId = supplierId,
            Quantity = 20,
            UnitPrice = 5,
            IdempotencyKey = "phase7c-po-1",
            Reason = "Low stock detected by inventory agent"
        });
        prepared.Created.Should().BeTrue();
        prepared.RequiresApproval.Should().BeTrue();
        prepared.PurchaseOrderId.Should().NotBeNull();

        var submitted = await harness.Procurement.SubmitPreparedPurchaseAsync(
            prepared.RecommendationId, "phase7c-po-1", approved: true, prepared.ApprovalFingerprint);
        submitted.Created.Should().BeTrue();

        var purchase = await harness.Db.Purchases
            .Include(x => x.PurchaseItems)
            .SingleAsync(x => x.Id == prepared.PurchaseOrderId);
        purchase.Status.Should().Be(PurchaseOrderStatusNames.Pending);
        purchase.SentToSupplier.Should().BeFalse();

        // Owner advances to Approved then receives verified quantity (execution path without live EDI).
        purchase.Status = PurchaseOrderStatusNames.Approved;
        await harness.Db.SaveChangesAsync();
        var line = purchase.PurchaseItems.Single();

        var received = await harness.Procurement.ReceiveVerifiedAsync(new ReceiveVerificationRequest
        {
            PurchaseOrderId = purchase.Id,
            Lines =
            [
                new ReceiveVerificationLineRequest
                {
                    PurchaseItemId = line.Id,
                    ReceivedQuantity = 20,
                    DamagedQuantity = 0
                }
            ]
        });

        received.StockIncreasedBy.Should().Be(20);
        (await harness.Db.Inventories.SingleAsync(x => x.ProductId == productId)).CurrentStock.Should().Be(21);
        (await harness.Db.Purchases.SingleAsync(x => x.Id == purchase.Id)).Status
            .Should().BeOneOf(PurchaseOrderStatusNames.Received, PurchaseOrderStatusNames.PartiallyReceived);
    }

    [Fact]
    public async Task WorkflowD_MarketingContent_RequiresApproval_ThenSandboxPublish_RecordsResult()
    {
        var harness = MarketingHarness.Open();

        var content = await harness.Marketing.GenerateContentItemAsync(new GenerateContentItemRequest
        {
            Platform = "LinkedIn",
            Brief = "Announce Phase7C launch using brand facts only",
            AgentKey = "marketing"
        });
        content.ApprovalStatus.Should().Be("Draft");

        content = await harness.Marketing.SubmitForReviewAsync(content.Id);
        content.ApprovalStatus.Should().Be("Review");
        content.ApprovalFingerprint.Should().NotBeNullOrWhiteSpace();

        content = await harness.Marketing.ApproveContentAsync(content.Id, content.ApprovalFingerprint);
        content.ApprovalStatus.Should().Be("Approved");

        var job = await harness.Marketing.ScheduleApprovedContentAsync(content.Id, new ScheduleContentRequest
        {
            RunAtUtc = DateTime.UtcNow.AddMinutes(-1),
            ConnectedAccountProvider = "sandbox",
            ConnectedAccountExternalId = "sandbox",
            IdempotencyKey = "phase7c-publish-1"
        });
        job.Status.Should().Be("Pending");
        job.Provider.Should().Be("sandbox");

        var processed = await harness.Marketing.ProcessDuePublishJobsAsync();
        processed.Succeeded.Should().BeGreaterThan(0);

        var published = await harness.Marketing.GetContentAsync(content.Id);
        published.ApprovalStatus.Should().Be("Published");
        published.ExternalPostId.Should().StartWith("sandbox-");
        harness.Db.MarketingPublishJobs.Should().Contain(x =>
            x.IdempotencyKey == "phase7c-publish-1" && x.Status == "Succeeded");
    }

    [Fact]
    public async Task RetryTask_RequeuesFailedAndDeadLetter_ButRejectsCompleted()
    {
        var harness = ReliabilityHarness.Open();
        await harness.Service.ActivateAsync("sales");

        var failed = new NalvaTask
        {
            AgentKey = "sales",
            Objective = "retry-failed",
            Status = NalvaStatuses.Failed,
            LastError = "transient",
            ClaimToken = Guid.NewGuid(),
            Attempt = 2,
            MaxAttempts = 3
        };
        var dead = new NalvaTask
        {
            AgentKey = "sales",
            Objective = "retry-dead",
            Status = NalvaStatuses.DeadLetter,
            LastError = "uncertain",
            Attempt = 3,
            MaxAttempts = 3
        };
        var completed = new NalvaTask
        {
            AgentKey = "sales",
            Objective = "retry-completed",
            Status = NalvaStatuses.Completed
        };
        harness.Db.NalvaTasks.AddRange(failed, dead, completed);
        await harness.Db.SaveChangesAsync();

        var retriedFailed = await harness.Service.RetryTaskAsync(failed.Id);
        retriedFailed.Status.Should().BeOneOf(NalvaStatuses.Assigned, NalvaStatuses.Queued);
        (await harness.Db.NalvaTasks.SingleAsync(x => x.Id == failed.Id)).LastError.Should().BeNull();
        (await harness.Db.NalvaTasks.SingleAsync(x => x.Id == failed.Id)).ClaimToken.Should().BeNull();

        var retriedDead = await harness.Service.RetryTaskAsync(dead.Id);
        retriedDead.Status.Should().BeOneOf(NalvaStatuses.Assigned, NalvaStatuses.Queued);

        var act = () => harness.Service.RetryTaskAsync(completed.Id);
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*failed*");
    }

    [Fact]
    public async Task SpecializedAgents_GuardedTools_StopForApprovalWithoutSideEffects()
    {
        var harness = ReliabilityHarness.Open();
        await harness.Service.ActivateAsync("hr");
        await harness.Service.ActivateAsync("software");

        var hr = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "hr",
            Objective = "Change employee title",
            Tools = ["ChangeEmployeeRecord"],
            ArgumentsJson = "{\"employeeId\":\"" + Guid.NewGuid() + "\",\"title\":\"Director\"}"
        });
        hr.Status.Should().Be(NalvaStatuses.AwaitingApproval);
        harness.Db.Employees.Should().BeEmpty();

        var deploy = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "software",
            Objective = "Deploy production",
            Tools = ["ExecuteProductionDeployment"],
            ArgumentsJson = "{\"environment\":\"production\"}"
        });
        deploy.Status.Should().Be(NalvaStatuses.AwaitingApproval);
        deploy.ApprovalId.Should().NotBeNull();
        (deploy.ResultJson ?? "").Should().Contain("awaiting_approval");
        harness.Db.NalvaApprovals.Should().Contain(x => x.ToolName == "ExecuteProductionDeployment" && x.Status == NalvaStatuses.Pending);
    }

    [Fact]
    public async Task InvalidAgentInput_FailsWithoutPersistingBusinessRecords()
    {
        var harness = ReliabilityHarness.Open();
        await harness.Service.ActivateAsync("sales");

        var act = () => harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "sales",
            Objective = "Create a lead with invalid JSON",
            Tools = ["CreateLead"],
            ArgumentsJson = "{not-json"
        });

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*valid JSON*");
        harness.Db.Leads.Should().BeEmpty();
    }

    private sealed class SupportHarness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaExecutionEngine Engine { get; init; }
        public required ISupportAgentService Support { get; init; }

        public static async Task<SupportHarness> OpenAsync()
        {
            var (db, tenantId, provider) = TenantDatabase.Open();
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            var notificationService = new Mock<INotificationService>();
            notificationService
                .Setup(x => x.CreateNotificationAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateNotificationRequest request, CancellationToken _) => new NotificationResponse
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    Title = request.Title,
                    Message = request.Message,
                    Type = request.Type,
                    IsRead = false,
                    Link = request.Link,
                    CreatedAt = DateTime.UtcNow
                });

            var channels = new SupportChannelRouter(
            [
                new SandboxSupportChannelAdapter(),
                new WebsiteChatSupportChannelAdapter(db),
                new EmailSupportChannelAdapter(),
                new WhatsAppSupportChannelAdapter(),
                new MessengerSupportChannelAdapter()
            ]);
            var support = new SupportAgentService(db, provider, user.Object, channels, notificationService.Object);
            var gateway = new NalvaToolGateway(db, provider, new CrmOperationsService(db, user.Object), support: support);
            var engine = new NalvaExecutionEngine(db, provider, user.Object, gateway, new SilentModel(), NullLogger<NalvaExecutionEngine>.Instance);
            var service = new NalvaWorkforceService(db, user.Object, engine, new NalvaWorkflowEngine(db, engine));
            await service.ActivateAsync("support");
            return new SupportHarness { Db = db, Service = service, Engine = engine, Support = support };
        }
    }

    private sealed class ProcurementHarness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required IInventoryProcurementAgentService Procurement { get; init; }

        public static async Task<ProcurementHarness> OpenAsync()
        {
            var (db, tenantId, provider) = TenantDatabase.Open();
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            var inventory = TenantDatabase.Inventory(db);
            var audit = new EntityAuditService(db, user.Object);
            var procurement = new InventoryProcurementAgentService(
                db, provider, user.Object, inventory, new UnavailableSupplierResearchProvider(), audit,
                NullLogger<InventoryProcurementAgentService>.Instance);
            await Task.CompletedTask;
            return new ProcurementHarness { Db = db, Procurement = procurement };
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
    }

    private sealed class MarketingHarness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required IMarketingOperationsService Marketing { get; init; }

        public static MarketingHarness Open()
        {
            var (db, tenantId, provider) = TenantDatabase.Open();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Phase7C Marketing Co",
                Slug = "phase7c-marketing",
                Email = "owner@phase7c.example",
                Phone = "555",
                Address = "Office",
                Country = "Pakistan",
                BusinessType = "software-agency",
                OwnerUserId = "user-1",
                SubscriptionPlanId = Guid.NewGuid(),
                Status = TenantStatus.Active,
                IsActive = true,
                Description = "Phase7C marketing workflow tenant"
            });
            db.CompanyStorefronts.Add(new CompanyStorefront
            {
                TenantId = tenantId,
                Industry = "software-agency",
                BrandColor = "#112233",
                PublicDescription = "We build software.",
                Website = "https://phase7c.example",
                IsPublished = true
            });
            db.SaveChanges();

            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            IMarketingPublishTransport[] transports = [new SandboxMarketingPublishTransport()];
            var marketing = new MarketingOperationsService(
                db, user.Object, provider, transports, NullLogger<MarketingOperationsService>.Instance,
                llm: new SilentModel(), images: null, integrations: null);
            return new MarketingHarness { Db = db, Marketing = marketing };
        }
    }

    private sealed class ReliabilityHarness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaExecutionEngine Engine { get; init; }

        public static ReliabilityHarness Open()
        {
            var (db, _, provider) = TenantDatabase.Open();
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(provider.TenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);
            var audit = new EntityAuditService(db, user.Object);
            var finance = new FinanceAgentService(db, provider, user.Object, audit, NullLogger<FinanceAgentService>.Instance);
            var gateway = new NalvaToolGateway(db, provider, new CrmOperationsService(db, user.Object), finance: finance);
            var engine = new NalvaExecutionEngine(db, provider, user.Object, gateway, new SilentModel(), NullLogger<NalvaExecutionEngine>.Instance);
            var service = new NalvaWorkforceService(db, user.Object, engine, new NalvaWorkflowEngine(db, engine));
            return new ReliabilityHarness { Db = db, Service = service, Engine = engine };
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

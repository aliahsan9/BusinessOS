using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Application.Features.Support;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Nalva;
using BusinessOS.Infrastructure.AI.Support;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Services;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class NalvaPhase5CSupportTests
{
    [Fact]
    public void SupportAgent_ExposesLifecycleToolsAndWorkflow()
    {
        NalvaAgentCatalog.TryGet("support", out var support).Should().BeTrue();
        support!.Tools.Should().Contain(["ReceiveSupportMessage", "SendSupportReply", "ResolveSupportTicket", "ScheduleSupportFollowUp", "EscalateTicket"]);
        NalvaWorkflowCatalog.TryGet("customer-support-automation", out var workflow).Should().BeTrue();
        workflow!.Steps.SelectMany(x => x.Tools).Should().Contain("ReceiveSupportMessage");
        NalvaToolCatalog.TryGet("IssueRefund", out var refund).Should().BeTrue();
        refund!.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task ProductInquiry_UsesAuthorizedKnowledgeAndSendsSandboxReply()
    {
        var harness = await Harness.OpenAsync();
        harness.Db.NalvaKnowledgeSources.Add(new NalvaKnowledgeSource
        {
            AgentKey = "support",
            Title = "Return policy",
            Content = "Items may be returned within 30 days with original receipt. Shipping fees are non-refundable.",
            CreatedByUserId = "user-1"
        });
        await harness.Db.SaveChangesAsync();

        var result = await harness.Support.ReceiveInboundAsync(new SupportInboundMessageRequest
        {
            Channel = SupportChannels.Sandbox,
            Body = "What is your return policy?",
            Subject = "Return question",
            IdempotencyKey = "inq-1",
            ProcessWithAgent = true
        });

        result.ResponseSent.Should().BeTrue();
        result.ResponseDeliveryStatus.Should().Be(SupportDeliveryStatuses.Simulated);
        result.Escalated.Should().BeFalse();

        var ticket = await harness.Support.GetTicketAsync(result.TicketId);
        ticket.Should().NotBeNull();
        ticket!.Messages.Should().Contain(x => x.Direction == SupportMessageDirections.Outbound && x.Body.Contains("30 days"));
        ticket.Status.Should().BeOneOf(SupportTicketStatuses.Resolved, SupportTicketStatuses.WaitingForCustomer, SupportTicketStatuses.InProgress);
        harness.Db.NalvaWorkItems.Should().Contain(x => x.Kind == "SupportTicket");
    }

    [Fact]
    public async Task OrderStatus_RequiresCustomerAuthorizationAndReturnsStoredOrder()
    {
        var harness = await Harness.OpenAsync();
        var customer = Customer("Sam", "Buyer", "sam@example.com", "555-0100");
        var other = Customer("Other", "Person", "other@example.com", "555-0101");
        harness.Db.Customers.AddRange(customer, other);
        var order = new Order
        {
            CustomerId = customer.Id,
            OrderNumber = "ORD-5C-100",
            Status = "Shipped",
            PaymentStatus = "Paid",
            GrandTotal = 120m,
            ShippingAddress = "12 Market Street"
        };
        harness.Db.Orders.Add(order);
        await harness.Db.SaveChangesAsync();

        var denied = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "support",
            Objective = "Check order for wrong customer",
            Tools = ["GetOrderDetails"],
            ArgumentsJson = $"{{\"orderId\":\"{order.Id}\",\"customerId\":\"{other.Id}\"}}"
        });
        denied.Status.Should().Be(NalvaStatuses.Completed);
        (denied.ResultJson ?? "").Should().Contain("authorized");

        var result = await harness.Support.ReceiveInboundAsync(new SupportInboundMessageRequest
        {
            Channel = SupportChannels.WebsiteChat,
            Body = "Where is my order ORD-5C-100?",
            CustomerEmail = "sam@example.com",
            OrderNumber = "ORD-5C-100",
            IdempotencyKey = "order-1",
            ProcessWithAgent = true
        });

        result.ResponseSent.Should().BeTrue();
        var ticket = await harness.Support.GetTicketAsync(result.TicketId);
        ticket!.RelatedOrderId.Should().Be(order.Id);
        ticket.Messages.Should().Contain(x => x.Body.Contains("ORD-5C-100") && x.Body.Contains("Shipped"));
    }

    [Fact]
    public async Task Complaint_CreatesTicketCategorizesAndEscalatesComplexRefund()
    {
        var harness = await Harness.OpenAsync();
        var customer = Customer("Alex", "Client", "alex@example.com", "555-0200");
        harness.Db.Customers.Add(customer);
        await harness.Db.SaveChangesAsync();

        var complaint = await harness.Support.ReceiveInboundAsync(new SupportInboundMessageRequest
        {
            Channel = SupportChannels.Sandbox,
            Body = "This is unacceptable. I want a full refund right now or I will call my lawyer.",
            CustomerId = customer.Id,
            Subject = "Angry refund demand",
            IdempotencyKey = "complaint-1"
        });

        complaint.Escalated.Should().BeTrue();
        var ticket = await harness.Support.GetTicketAsync(complaint.TicketId);
        ticket!.Status.Should().Be(SupportTicketStatuses.Escalated);
        ticket.Category.Should().BeOneOf("Returns", "Complaint");
        ticket.Priority.Should().BeOneOf(SupportPriorities.High, SupportPriorities.Urgent);
        ticket.EscalationReason.Should().NotBeNullOrWhiteSpace();
        harness.Notifications.Should().Contain(x => x.Type == "SupportEscalation");
    }

    [Fact]
    public async Task DuplicateInbound_IsIdempotent_AndFollowUpCanSend()
    {
        var harness = await Harness.OpenAsync();
        var first = await harness.Support.ReceiveInboundAsync(new SupportInboundMessageRequest
        {
            Channel = SupportChannels.Sandbox,
            Body = "I need help with shipping times to Karachi.",
            IdempotencyKey = "dup-1"
        });
        var second = await harness.Support.ReceiveInboundAsync(new SupportInboundMessageRequest
        {
            Channel = SupportChannels.Sandbox,
            Body = "I need help with shipping times to Karachi.",
            IdempotencyKey = "dup-1"
        });

        second.TicketId.Should().Be(first.TicketId);
        harness.Db.SupportMessages.Count(x => x.IdempotencyKey == "dup-1").Should().Be(1);

        await harness.Support.ScheduleFollowUpAsync(first.TicketId, new SupportFollowUpRequest
        {
            Kind = "PendingInfo",
            MessageTemplate = "Please share your city so we can confirm shipping times.",
            DueAt = DateTime.UtcNow.AddMinutes(-1),
            DedupKey = "follow-1",
            RespectBusinessHours = false,
            Channel = SupportChannels.Sandbox
        });

        var processed = await harness.Support.ProcessDueFollowUpsAsync();
        processed.Should().BeGreaterThan(0);
        harness.Db.SupportFollowUpJobs.Should().Contain(x => x.DedupKey == "follow-1" && x.Status == SupportFollowUpStatuses.Sent);
    }

    [Fact]
    public async Task CrossTenant_CustomerAndOrderAreNotLeaked()
    {
        var left = await Harness.OpenAsync("tenant-left");
        var right = await Harness.OpenAsync("tenant-right");

        var foreignCustomer = Customer("Foreign", "Customer", "foreign@example.com", "555-9999");
        // Bypass query filter assignment by setting tenant explicitly after add through SaveChanges on left context.
        left.Db.Customers.Add(foreignCustomer);
        await left.Db.SaveChangesAsync();

        var context = await right.Support.GetAuthorizedCustomerContextAsync(foreignCustomer.Id, "foreign@example.com", null, null, null);
        context["customerFound"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task Overview_ReportsPersistedMetricsOnly()
    {
        var harness = await Harness.OpenAsync();
        await harness.Support.ReceiveInboundAsync(new SupportInboundMessageRequest
        {
            Channel = SupportChannels.Sandbox,
            Body = "Hello, do you ship internationally?",
            IdempotencyKey = "metrics-1"
        });

        var overview = await harness.Support.GetOverviewAsync();
        overview.TotalTickets.Should().BeGreaterThan(0);
        overview.Channels.Should().Contain(x => x.Channel == SupportChannels.Sandbox && x.Status == SupportChannelConnectionStatuses.Sandbox);
        overview.Channels.Should().Contain(x => x.Channel == SupportChannels.Messenger && x.Status == SupportChannelConnectionStatuses.Unavailable);
        overview.CostIsEstimate.Should().BeTrue();
    }

    [Fact]
    public async Task OwnerApprovalPath_IssueRefundStaysPreparedWithoutPaymentChange()
    {
        var harness = await Harness.OpenAsync();
        await harness.Service.ActivateAsync("support");
        var run = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "support",
            Objective = "Request a refund",
            Tools = ["IssueRefund"],
            ArgumentsJson = "{\"paymentId\":\"00000000-0000-0000-0000-000000000001\",\"amount\":10}"
        });

        run.Status.Should().Be(NalvaStatuses.AwaitingApproval);
        harness.Db.NalvaApprovals.Should().Contain(x => x.Action == "IssueRefund");
    }

    [Fact]
    public async Task NavlaDelegation_SupportObjectiveCreatesTicketThroughTool()
    {
        var harness = await Harness.OpenAsync();
        await harness.Service.ActivateAsync("support");
        await harness.Service.ActivateAsync("ceo");

        harness.Db.NalvaKnowledgeSources.Add(new NalvaKnowledgeSource
        {
            AgentKey = null,
            Title = "Shipping",
            Content = "Standard shipping takes 3 to 5 business days inside Pakistan.",
            CreatedByUserId = "user-1"
        });
        await harness.Db.SaveChangesAsync();

        var run = await harness.Engine.ExecuteAsync(new NalvaExecuteRequest
        {
            AgentKey = "support",
            Objective = "Handle routine customer inquiries about shipping and notify me about serious complaints.",
            Tools = ["ReceiveSupportMessage", "SearchKnowledgeBase"],
            ArgumentsJson = "{\"body\":\"How long does shipping take?\",\"channel\":\"sandbox\",\"idempotencyKey\":\"navla-1\"}"
        });

        run.Status.Should().Be(NalvaStatuses.Completed);
        harness.Db.SupportTickets.Should().NotBeEmpty();
        var ticket = harness.Db.SupportTickets.OrderByDescending(x => x.CreatedAt).First();
        ticket.Messages.Should().NotBeEmpty();
    }

    private static Customer Customer(string first, string last, string email, string phone) => new()
    {
        FirstName = first,
        LastName = last,
        Email = email,
        PhoneNumber = phone,
        Address = "1 Test Street",
        City = "Lahore",
        Country = "Pakistan",
        PostalCode = "54000",
        Status = "Active"
    };

    private sealed class Harness
    {
        public required BusinessOSDbContext Db { get; init; }
        public required NalvaWorkforceService Service { get; init; }
        public required NalvaExecutionEngine Engine { get; init; }
        public required ISupportAgentService Support { get; init; }
        public required List<CreateNotificationRequest> Notifications { get; init; }

        public static async Task<Harness> OpenAsync(string? database = null)
        {
            var (db, tenantId, provider) = TenantDatabase.Open(name: database);
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(x => x.UserId).Returns("user-1");
            user.SetupGet(x => x.TenantId).Returns(tenantId);
            user.Setup(x => x.HasPermission(It.IsAny<string>())).Returns(true);

            var notifications = new List<CreateNotificationRequest>();
            var notificationService = new Mock<INotificationService>();
            notificationService
                .Setup(x => x.CreateNotificationAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateNotificationRequest request, CancellationToken _) =>
                {
                    notifications.Add(request);
                    return new NotificationResponse
                    {
                        Id = Guid.NewGuid(),
                        UserId = request.UserId,
                        Title = request.Title,
                        Message = request.Message,
                        Type = request.Type,
                        IsRead = false,
                        Link = request.Link,
                        CreatedAt = DateTime.UtcNow
                    };
                });

            // local helper continues below

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

            return new Harness
            {
                Db = db,
                Service = service,
                Engine = engine,
                Support = support,
                Notifications = notifications
            };
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

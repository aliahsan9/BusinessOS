using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.AI.Copilot;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class AiMemoryServiceTests
{
    [Fact]
    public async Task CreateAndCompleteTurn_PersistsChronologicalHistory()
    {
        var harness = CreateHarness();
        var request = new AiChatRequest("Show me my top 5 selling products this month.");
        var sessionId = await harness.Service.GetOrCreateSessionAsync(request);

        await harness.Service.CompleteTurnAsync(
            sessionId,
            request,
            AiCopilotIntent.Analytics,
            request.Message,
            "Laptop, Mouse, Keyboard, Monitor and Headphones.");

        var messages = await harness.Service.GetSessionMessagesAsync(sessionId);
        messages.Should().HaveCount(1);
        messages[0].Prompt.Should().Contain("top 5");
        messages[0].Response.Should().Contain("Laptop");

        var memory = await harness.Service.LoadAsync(sessionId);
        memory.RecentTurns.Should().HaveCount(2);
        memory.RecentTurns[0].Role.Should().Be(AiMessageRoles.User);
        memory.RecentTurns[1].Role.Should().Be(AiMessageRoles.Assistant);
    }

    [Fact]
    public async Task CompleteTurn_PreservesPendingActionUntilCleared()
    {
        var harness = CreateHarness();
        var request = new AiChatRequest("Create a new customer");
        var sessionId = await harness.Service.GetOrCreateSessionAsync(request);
        const string draft = "{\"tool\":\"CreateCustomer\"}";

        await harness.Service.CompleteTurnAsync(
            sessionId,
            request,
            AiCopilotIntent.ActionCreate,
            request.Message,
            "I need a name, phone, and email.",
            updatePendingAction: true,
            pendingActionJson: draft);

        var saved = await harness.Service.LoadAsync(sessionId);
        saved.PendingActionJson.Should().Contain("CreateCustomer");

        await harness.Service.CompleteTurnAsync(
            sessionId,
            new AiChatRequest("Thanks"),
            AiCopilotIntent.Conversational,
            "Thanks",
            "Anytime.");

        var kept = await harness.Service.LoadAsync(sessionId);
        kept.PendingActionJson.Should().Contain("CreateCustomer");

        await harness.Service.SetPendingActionAsync(sessionId, null);
        var cleared = await harness.Service.LoadAsync(sessionId);
        cleared.PendingActionJson.Should().BeNull();
    }

    [Fact]
    public async Task FollowUpLoad_IncludesPreviousTurns()
    {
        var harness = CreateHarness();
        var first = new AiChatRequest("Analyze my inventory.");
        var sessionId = await harness.Service.GetOrCreateSessionAsync(first);
        await harness.Service.CompleteTurnAsync(sessionId, first, AiCopilotIntent.Analytics, first.Message, "SKU A is low.");

        var memory = await harness.Service.LoadAsync(sessionId);
        memory.RecentTurns.Select(t => t.Content).Should().Contain(c => c.Contains("SKU A"));
    }

    [Fact]
    public async Task NewConversation_DoesNotInheritOtherHistory()
    {
        var harness = CreateHarness();
        var first = new AiChatRequest("First conversation");
        var a = await harness.Service.GetOrCreateSessionAsync(first);
        await harness.Service.CompleteTurnAsync(a, first, AiCopilotIntent.Conversational, first.Message, "Answer A");

        var second = new AiChatRequest("Second conversation");
        var b = await harness.Service.GetOrCreateSessionAsync(second);
        var memory = await harness.Service.LoadAsync(b);
        memory.RecentTurns.Should().BeEmpty();
    }

    [Fact]
    public async Task UserCannotLoadAnotherUsersConversation()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var owner = CreateService(context, tenantId, "owner");
        var other = CreateService(context, tenantId, "intruder");
        var request = new AiChatRequest("Private notes");
        var sessionId = await owner.GetOrCreateSessionAsync(request);
        await owner.CompleteTurnAsync(sessionId, request, AiCopilotIntent.Conversational, request.Message, "Secret");

        var leaked = await other.LoadAsync(sessionId);
        leaked.RecentTurns.Should().BeEmpty();

        var deleted = await other.DeleteSessionAsync(sessionId);
        deleted.Should().BeFalse();

        var messages = await other.GetSessionMessagesAsync(sessionId);
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidConversationId_CreatesNewSession()
    {
        var harness = CreateHarness();
        var unknown = Guid.NewGuid();
        var created = await harness.Service.GetOrCreateSessionAsync(new AiChatRequest("Hello"), unknown);
        created.Should().NotBe(unknown);
    }

    [Fact]
    public async Task DuplicateClientRequestId_DoesNotPersistSecondTurn()
    {
        var harness = CreateHarness();
        var request = new AiChatRequest("Ping");
        var sessionId = await harness.Service.GetOrCreateSessionAsync(request);
        await harness.Service.CompleteTurnAsync(
            sessionId, request, AiCopilotIntent.Conversational, "Ping", "Pong",
            clientRequestId: "req-1");
        await harness.Service.CompleteTurnAsync(
            sessionId, request, AiCopilotIntent.Conversational, "Ping", "Pong again",
            clientRequestId: "req-1");

        var messages = await harness.Service.GetSessionMessagesAsync(sessionId);
        messages.Should().HaveCount(1);
        messages[0].Response.Should().Be("Pong");
    }

    [Fact]
    public async Task DeleteConversation_HidesItFromOwner()
    {
        var harness = CreateHarness();
        var request = new AiChatRequest("Soon gone");
        var sessionId = await harness.Service.GetOrCreateSessionAsync(request);
        (await harness.Service.DeleteSessionAsync(sessionId)).Should().BeTrue();
        (await harness.Service.GetSessionAsync(sessionId)).Should().BeNull();
    }

    [Fact]
    public async Task EmptyConversation_CanBeListed()
    {
        var harness = CreateHarness();
        var id = await harness.Service.CreateSessionAsync();
        var listed = await harness.Service.ListSessionsAsync(10);
        listed.Should().Contain(s => s.Id == id);
        listed.First(s => s.Id == id).MessageCount.Should().Be(0);
    }

    [Fact]
    public async Task ToolResults_AreStoredAsHistoricalContext_NotLiveReplay()
    {
        var harness = CreateHarness();
        var request = new AiChatRequest("Analyze inventory");
        var sessionId = await harness.Service.GetOrCreateSessionAsync(request);
        await harness.Service.CompleteTurnAsync(
            sessionId,
            request,
            AiCopilotIntent.Analytics,
            request.Message,
            "Reorder SKU A first.",
            toolsUsed: ["GetInventorySummary"],
            toolResults:
            [
                new AiToolResult
                {
                    ToolName = "GetInventorySummary",
                    Summary = "SKU A has 2 units remaining.",
                    Data = new { secret = "do-not-store" }
                }
            ]);

        var memory = await harness.Service.LoadAsync(sessionId);
        memory.RecentTurns.Should().Contain(t => t.Role == AiMessageRoles.Tool && t.Content.Contains("not live data"));
        memory.RecentTurns.Should().NotContain(t => t.Content.Contains("do-not-store"));
    }

    [Fact]
    public async Task UpdateSummary_ExcludesCoveredMessagesFromRecentWindow()
    {
        var harness = CreateHarness();
        var request = new AiChatRequest("Start");
        var sessionId = await harness.Service.GetOrCreateSessionAsync(request);
        await harness.Service.CompleteTurnAsync(sessionId, request, AiCopilotIntent.Conversational, "Start", "First");
        await harness.Service.CompleteTurnAsync(sessionId, new AiChatRequest("Next"), AiCopilotIntent.Conversational, "Next", "Second");

        var before = await harness.Service.LoadAsync(sessionId);
        var lastCovered = before.RecentTurns[1].SequenceNumber;
        await harness.Service.UpdateSummaryAsync(sessionId, "Discussed start of the analysis.", lastCovered);

        var after = await harness.Service.LoadAsync(sessionId);
        after.ConversationSummary.Should().Contain("analysis");
        after.RecentTurns.Should().OnlyContain(t => t.SequenceNumber > lastCovered);
    }

    private static (AiMemoryService Service, Guid TenantId) CreateHarness(string userId = "user-a")
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        return (CreateService(context, tenantId, userId), tenantId);
    }

    private static AiMemoryService CreateService(
        global::BusinessOS.Infrastructure.Data.BusinessOSDbContext context,
        Guid tenantId,
        string userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);

        var contextService = new Mock<IAiContextService>();
        contextService.Setup(x => x.BuildPageContext(It.IsAny<AiChatRequest>()))
            .Returns(new AiPageContextDto { Url = "/ai/workspace", Module = "ai" });

        return new AiMemoryService(
            context,
            currentUser.Object,
            contextService.Object,
            Options.Create(new AiMemoryOptions()),
            NullLogger<AiMemoryService>.Instance);
    }
}

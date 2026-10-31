using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.AI.Copilot;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class AiConversationSummarizerTests
{
    [Fact]
    public async Task TrySummarize_PersistsSummary_WithoutDuplicatingRecentTurns()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var userId = "user-a";
        var sessionId = Guid.NewGuid();
        context.AiConversationSessions.Add(new AiConversationSession
        {
            Id = sessionId,
            TenantId = tenantId,
            UserId = userId,
            Title = "Sales",
            IsActive = true
        });

        for (var i = 1; i <= 24; i++)
        {
            context.AiMessages.Add(new AiMessage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                ConversationId = sessionId,
                Role = i % 2 == 1 ? AiMessageRoles.User : AiMessageRoles.Assistant,
                Content = $"Turn {i}",
                SequenceNumber = i
            });
        }

        await context.SaveChangesAsync();

        var llm = new Mock<ILlmChatClient>();
        llm.SetupGet(x => x.IsConfigured).Returns(true);
        llm.Setup(x => x.GenerateReplyAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyList<AiMemoryTurnDto>>()))
            .ReturnsAsync("The user analyzed monthly sales and asked about low-margin products.");

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);

        var summarizer = new AiConversationSummarizer(
            context,
            currentUser.Object,
            llm.Object,
            Options.Create(new AiMemoryOptions
            {
                Enabled = true,
                SummarizationEnabled = true,
                SummarizationThreshold = 20,
                RecentMessageLimit = 10,
                MaxSummaryTokens = 100
            }),
            NullLogger<AiConversationSummarizer>.Instance);

        await summarizer.TrySummarizeAsync(sessionId);

        var session = context.AiConversationSessions.Single(s => s.Id == sessionId);
        session.Summary.Should().Contain("monthly sales");
        session.LastSummarizedMessageSequence.Should().BeGreaterThan(0);

        var memory = new AiMemoryService(
            context,
            currentUser.Object,
            Mock.Of<IAiContextService>(c => c.BuildPageContext(It.IsAny<AiChatRequest>()) == new AiPageContextDto()),
            Options.Create(new AiMemoryOptions { RecentMessageLimit = 10 }),
            NullLogger<AiMemoryService>.Instance);

        var loaded = await memory.LoadAsync(sessionId);
        loaded.ConversationSummary.Should().Contain("monthly sales");
        loaded.RecentTurns.Should().OnlyContain(t => t.SequenceNumber > session.LastSummarizedMessageSequence);
    }

    [Fact]
    public async Task TrySummarize_LlmFailure_LeavesConversationUsable()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var userId = "user-a";
        var sessionId = Guid.NewGuid();
        context.AiConversationSessions.Add(new AiConversationSession
        {
            Id = sessionId,
            TenantId = tenantId,
            UserId = userId,
            Title = "Sales",
            IsActive = true
        });
        for (var i = 1; i <= 22; i++)
        {
            context.AiMessages.Add(new AiMessage
            {
                TenantId = tenantId,
                UserId = userId,
                ConversationId = sessionId,
                Role = AiMessageRoles.Assistant,
                Content = "x",
                SequenceNumber = i
            });
        }
        await context.SaveChangesAsync();

        var llm = new Mock<ILlmChatClient>();
        llm.SetupGet(x => x.IsConfigured).Returns(true);
        llm.Setup(x => x.GenerateReplyAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<AiMemoryTurnDto>>()))
            .ThrowsAsync(new InvalidOperationException("LLM down"));

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);

        var summarizer = new AiConversationSummarizer(
            context,
            currentUser.Object,
            llm.Object,
            Options.Create(new AiMemoryOptions { SummarizationThreshold = 20, RecentMessageLimit = 4 }),
            NullLogger<AiConversationSummarizer>.Instance);

        await summarizer.Invoking(s => s.TrySummarizeAsync(sessionId)).Should().NotThrowAsync();
        context.AiConversationSessions.Single().Summary.Should().BeNull();
    }
}

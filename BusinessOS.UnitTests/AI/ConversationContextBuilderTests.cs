using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.AI.ConversationMemory;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Domain.Entities;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class ConversationContextBuilderTests
{
    private static AiMemoryOptions Options => new()
    {
        Enabled = true,
        RecentMessageLimit = 4,
        MaxHistoryTokens = 200,
        SummarizationEnabled = true,
        SummarizationThreshold = 6,
        MaxSummaryTokens = 40
    };

    [Fact]
    public void Build_OmitsSummarizedMessages_AndKeepsRecentWindow()
    {
        var messages = Enumerable.Range(1, 8)
            .Select(i => new AiMemoryTurnDto
            {
                Role = i % 2 == 1 ? AiMessageRoles.User : AiMessageRoles.Assistant,
                Content = $"Message {i} about products",
                SequenceNumber = i
            })
            .ToList();

        var context = ConversationContextBuilder.Build("Older sales discussion.", 4, messages, Options);

        context.Summary.Should().Be("Older sales discussion.");
        context.RecentTurns.Should().OnlyContain(t => t.SequenceNumber > 4);
        context.RecentTurns.Count.Should().BeLessThanOrEqualTo(4);
        context.HistoryTokenEstimate.Should().BeGreaterThan(0);
        context.HistoryTokenEstimate.Should().BeLessThanOrEqualTo(Options.MaxHistoryTokens + 80);
    }

    [Fact]
    public void Build_Disabled_ReturnsEmptyContext()
    {
        var options = new AiMemoryOptions { Enabled = false };
        var context = ConversationContextBuilder.Build(
            "summary",
            0,
            [new AiMemoryTurnDto { Role = "user", Content = "Hi", SequenceNumber = 1 }],
            options);

        context.RecentTurns.Should().BeEmpty();
        context.Summary.Should().BeNull();
    }

    [Fact]
    public void Build_RespectsTokenBudget()
    {
        var options = new AiMemoryOptions
        {
            Enabled = true,
            RecentMessageLimit = 10,
            MaxHistoryTokens = 30
        };

        var messages = Enumerable.Range(1, 10)
            .Select(i => new AiMemoryTurnDto
            {
                Role = "user",
                Content = new string('a', 200),
                SequenceNumber = i
            })
            .ToList();

        var context = ConversationContextBuilder.Build(null, 0, messages, options);

        context.RecentTurns.Should().NotBeEmpty();
        context.RecentTurns.Count.Should().BeLessThan(messages.Count);
        context.HistoryTokenEstimate.Should().BeLessThanOrEqualTo(options.MaxHistoryTokens + 80);
    }

    [Fact]
    public void SelectMessagesToSummarize_WaitsForThreshold()
    {
        var messages = Enumerable.Range(1, 5)
            .Select(i => new AiMemoryTurnDto { Role = "user", Content = "x", SequenceNumber = i })
            .ToList();

        ConversationContextBuilder.SelectMessagesToSummarize(messages, 0, Options)
            .Should().BeEmpty();
    }

    [Fact]
    public void SelectMessagesToSummarize_SkipsTrailingToolMessage()
    {
        var messages = new List<AiMemoryTurnDto>();
        var seq = 1;
        for (var i = 0; i < 8; i++)
        {
            messages.Add(new AiMemoryTurnDto { Role = AiMessageRoles.User, Content = "u", SequenceNumber = seq++ });
            messages.Add(new AiMemoryTurnDto { Role = AiMessageRoles.Assistant, Content = "a", SequenceNumber = seq++ });
        }

        messages.Add(new AiMemoryTurnDto { Role = AiMessageRoles.Tool, Content = "partial", SequenceNumber = seq });

        var selected = ConversationContextBuilder.SelectMessagesToSummarize(messages, 0, Options);
        selected.Should().NotBeEmpty();
        selected[^1].Role.Should().NotBe(AiMessageRoles.Tool);
    }

    [Fact]
    public void TitleFormatter_UsesDeterministicTitle()
    {
        ConversationTitleFormatter.FromFirstUserMessage(
                "Can you analyze my monthly sales and identify my top products?")
            .Should().Contain("Monthly Sales");
    }

    [Fact]
    public void TokenEstimator_EmptyIsZero()
    {
        ConversationTokenEstimator.EstimateTokens(null).Should().Be(0);
        ConversationTokenEstimator.EstimateTokens("abcd").Should().Be(1);
    }
}

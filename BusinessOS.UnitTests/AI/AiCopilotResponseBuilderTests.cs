using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Copilot;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AiCopilotResponseBuilderTests
{
    [Fact]
    public void BuildNoDataReply_DoesNotInventNumbers()
    {
        var reply = AiCopilotResponseBuilder.BuildNoDataReply(
            "What is our revenue this month?",
            AiCopilotIntent.Analytics,
            []);

        reply.Should().Contain("won't invent");
        reply.Should().Contain("couldn't find");
        reply.Should().NotMatchRegex(@"\$\d");
    }

    [Fact]
    public void BuildFromTools_AppendsCitations()
    {
        var reply = AiCopilotResponseBuilder.BuildFromTools(
            "Show refund policy",
            [new AiToolResult { ToolName = "SearchDocuments", Summary = "Refunds within 14 days." }],
            [new AiCitationDto { Title = "Refund Policy", DocumentType = "Policy", Score = 0.91 }],
            new AiMemoryStateDto());

        reply.Should().Contain("Refunds within 14 days.");
        reply.Should().Contain("Sources:");
        reply.Should().Contain("Refund Policy");
    }

    [Fact]
    public void BuildGroundedAdviceReply_IncludesLiveDataBlock()
    {
        var reply = AiCopilotResponseBuilder.BuildGroundedAdviceReply(
            "How can I increase sales?",
            [
                new AiToolResult
                {
                    ToolName = "GetBestSellingProducts",
                    Summary = "Best-selling products this month:\n1. Laptop Pro: 40 units, $20,000.00 revenue"
                }
            ],
            []);

        reply.Should().Contain("Laptop Pro");
        reply.Should().Contain("Practical next steps");
        reply.Should().Contain("grounded in your live data");
    }

    [Fact]
    public void BuildLlmUserPrompt_IncludesGroundingRules()
    {
        var prompt = AiCopilotResponseBuilder.BuildLlmUserPrompt(
            "What sold best?",
            new AiPageContextDto(),
            new AiMemoryStateDto(),
            [new AiToolResult { ToolName = "GetBestSellingProducts", Summary = "No product sales found this month." }],
            []);

        prompt.Should().Contain("groundingRules");
        prompt.Should().Contain("never invent metrics");
        prompt.Should().Contain("What sold best?");
    }
}

using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Copilot;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AiIntentDetectorTests
{
    private readonly AiIntentDetector _detector = new();
    private static AiPageContextDto Page => new();
    private static AiMemoryStateDto Memory => new();

    [Theory]
    [InlineData("Which products are best selling this month?")]
    [InlineData("What are our bestsellers?")]
    [InlineData("Show top selling products")]
    public void Detect_BestSellerQueries_RoutesToAnalyticsWithBestSellerTool(string message)
    {
        var result = _detector.Detect(message, Page, Memory);

        result.Intent.Should().Be(AiCopilotIntent.Analytics);
        result.SuggestedTools.Should().Contain(AiToolName.GetBestSellingProducts);
    }

    [Theory]
    [InlineData("What are our sales trends?")]
    [InlineData("Are sales growing vs last month?")]
    [InlineData("Show future trends based on recent sales")]
    public void Detect_TrendQueries_RoutesToAnalyticsWithTrendsTool(string message)
    {
        var result = _detector.Detect(message, Page, Memory);

        result.Intent.Should().Be(AiCopilotIntent.Analytics);
        result.SuggestedTools.Should().Contain(AiToolName.GetSalesTrends);
    }

    [Fact]
    public void Detect_IncreaseSalesAdvice_IsConversationalWithGroundingTools()
    {
        var result = _detector.Detect("How can I increase sales based on our current data?", Page, Memory);

        result.Intent.Should().Be(AiCopilotIntent.Conversational);
        result.SuggestedTools.Should().Contain(AiToolName.GetSalesSummary);
        result.SuggestedTools.Should().Contain(AiToolName.GetBestSellingProducts);
        result.SuggestedTools.Should().Contain(AiToolName.GetSalesTrends);
    }

    [Fact]
    public void Detect_RevenueThisMonth_IsAnalytics()
    {
        var result = _detector.Detect("What is our revenue this month?", Page, Memory);

        result.Intent.Should().Be(AiCopilotIntent.Analytics);
        result.SuggestedTools.Should().Contain(AiToolName.GetRevenue);
    }

    [Fact]
    public void Detect_ShowTopSelling_DoesNotMisclassifyAsHelp()
    {
        // Regression: "show top" contains the substring "how to".
        var result = _detector.Detect("Show top selling products", Page, Memory);

        result.Intent.Should().Be(AiCopilotIntent.Analytics);
        result.SuggestedTools.Should().Contain(AiToolName.GetBestSellingProducts);
    }

    [Fact]
    public void Detect_Greeting_IsConversationalWithoutTools()
    {
        var result = _detector.Detect("Hello!", Page, Memory);

        result.Intent.Should().Be(AiCopilotIntent.Conversational);
        result.SuggestedTools.Should().BeEmpty();
    }

    [Fact]
    public void Detect_DocumentSearch_UsesSearchDocumentsTool()
    {
        var result = _detector.Detect("Find the refund policy document", Page, Memory);

        result.Intent.Should().Be(AiCopilotIntent.DocumentSearch);
        result.SuggestedTools.Should().Contain(AiToolName.SearchDocuments);
    }

    [Theory]
    [InlineData("Which one generated the highest revenue?")]
    [InlineData("What about last month?")]
    [InlineData("Continue your previous analysis.")]
    public void Detect_FollowUpPhrases_UsesConversationMemory(string message)
    {
        var memory = new AiMemoryStateDto
        {
            LastIntent = nameof(AiCopilotIntent.Analytics),
            RecentTurns =
            [
                new AiMemoryTurnDto { Role = "user", Content = "Show me my top 5 selling products this month.", SequenceNumber = 1 },
                new AiMemoryTurnDto { Role = "assistant", Content = "Laptop, Mouse, Keyboard, Monitor, Headphones.", SequenceNumber = 2 }
            ]
        };

        var result = _detector.Detect(message, Page, memory);

        result.Intent.Should().Be(AiCopilotIntent.FollowUp);
    }

    [Fact]
    public void Detect_FollowUp_WithoutHistory_DoesNotForceFollowUp()
    {
        var result = _detector.Detect("Which one generated the highest revenue?", Page, Memory);

        result.Intent.Should().NotBe(AiCopilotIntent.FollowUp);
    }
}

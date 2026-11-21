using BusinessOS.Application.Features.Agents;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Agents;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class NavlaSpecialistRouterTests
{
    [Theory]
    [InlineData("Which products are running low?", NavlaSpecialistKeys.Inventory)]
    [InlineData("Meray paas kitna stock baqi hai?", NavlaSpecialistKeys.Inventory)]
    [InlineData("How much did I earn this month?", NavlaSpecialistKeys.Finance)]
    [InlineData("Aaj kitni sales hui hain?", NavlaSpecialistKeys.Sales)]
    [InlineData("Show my highest-selling products", NavlaSpecialistKeys.Sales)]
    [InlineData("Which customers purchased the most this year?", NavlaSpecialistKeys.Customers)]
    [InlineData("Search the employee handbook policy", NavlaSpecialistKeys.Knowledge)]
    [InlineData("Show pending orders", NavlaSpecialistKeys.Orders)]
    public void Routes_domain_questions_to_expected_specialist(string message, string expected)
    {
        var intent = new AiIntentDetectionResult
        {
            Intent = AiCopilotIntent.ActionRead,
            Confidence = 0.9,
            SuggestedTools = []
        };

        var route = NavlaSpecialistRouter.Route(message, intent);

        route.SpecialistKeys.Should().Contain(expected);
        route.DisplayLabel.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Multi_module_question_selects_sales_and_inventory()
    {
        var intent = new AiIntentDetectionResult
        {
            Intent = AiCopilotIntent.Analytics,
            Confidence = 0.9,
            SuggestedTools =
            [
                AiToolName.GetBestSellingProducts,
                AiToolName.GetLowStock,
                AiToolName.GetInventorySummary
            ]
        };

        var route = NavlaSpecialistRouter.Route(
            "Show me my top-selling products and their current stock.",
            intent);

        route.IsMultiModule.Should().BeTrue();
        route.SpecialistKeys.Should().Contain(NavlaSpecialistKeys.Sales);
        route.SpecialistKeys.Should().Contain(NavlaSpecialistKeys.Inventory);
        route.AllowedTools.Should().Contain(AiToolName.GetBestSellingProducts);
        route.AllowedTools.Should().Contain(AiToolName.GetInventorySummary);
    }

    [Fact]
    public void Apply_keeps_detector_tools_when_intersection_empty()
    {
        var intent = new AiIntentDetectionResult
        {
            Intent = AiCopilotIntent.ActionRead,
            Confidence = 0.9,
            SuggestedTools = [AiToolName.GetDailyPriorities]
        };
        var route = new NavlaSpecialistRoute(
            [NavlaSpecialistKeys.Knowledge],
            [AiToolName.SearchDocuments],
            "Knowledge Agent",
            false);

        var applied = NavlaSpecialistRouter.Apply(intent, route);

        applied.SuggestedTools.Should().Contain(AiToolName.GetDailyPriorities);
    }

    [Fact]
    public void Ambiguous_smalltalk_defaults_to_business_assistant()
    {
        var intent = new AiIntentDetectionResult
        {
            Intent = AiCopilotIntent.Conversational,
            Confidence = 0.95,
            SuggestedTools = []
        };

        var route = NavlaSpecialistRouter.Route("hello", intent);

        route.SpecialistKeys.Should().ContainSingle()
            .Which.Should().Be(NavlaSpecialistKeys.BusinessAssistant);
    }
}

using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using BusinessOS.Infrastructure.AI.Copilot;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AgentIntentParserTests
{
    private readonly AgentIntentParser _parser = new(new AiIntentDetector());
    private static AiPageContextDto Page => new() { Module = "customers", Url = "/customers" };
    private static AiMemoryStateDto Memory => new();

    [Theory]
    [InlineData("Create customer Ahmed Ali phone 03001234567")]
    [InlineData("Add a new client named Sara")]
    [InlineData("Register customer")]
    public void Parse_CreateCustomerVariants_SuggestsCreateCustomer(string message)
    {
        var result = _parser.Parse(message, Page, Memory, "en");

        result.Intent.Should().Be(AiCopilotIntent.ActionCreate);
        result.SuggestedTools.Should().Contain(AiToolName.CreateCustomer);
    }

    [Fact]
    public void Parse_UrduCreateCustomer_NormalizesAndSuggestsCreateCustomer()
    {
        var result = _parser.Parse("صوفیہ ایک نیا کسٹمر بناؤ نام علی احسن فون 03001234567", Page, Memory, "ur");

        result.Intent.Should().Be(AiCopilotIntent.ActionCreate);
        result.SuggestedTools.Should().Contain(AiToolName.CreateCustomer);
    }

    [Fact]
    public void NormalizeBilingual_MapsUrduPhrases()
    {
        var normalized = AgentIntentParser.NormalizeBilingual("نیا گاہک بناؤ");
        normalized.Should().Contain("create customer");
    }

    [Fact]
    public void Parse_PageAwareCreateOne_UsesCustomersPage()
    {
        var result = _parser.Parse("create one", Page, Memory, "en");

        result.Intent.Should().Be(AiCopilotIntent.ActionCreate);
        result.SuggestedTools.Should().Contain(AiToolName.CreateCustomer);
    }

    [Fact]
    public void Parse_ShowProfit_SuggestsShowProfit()
    {
        var result = _parser.Parse("Show profit for this month", new AiPageContextDto { Module = "finance" }, Memory, "en");

        result.SuggestedTools.Should().Contain(AiToolName.ShowProfit);
    }
}

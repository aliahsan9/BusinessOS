using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AgentToolRiskClassifierTests
{
    [Fact]
    public void EveryToolHasAValidRiskClassification()
    {
        foreach (var tool in Enum.GetValues<AiToolName>())
        {
            var risk = AgentToolRiskClassifier.Classify(tool);
            Enum.IsDefined(risk).Should().BeTrue($"{tool} must have a valid risk level");
        }
    }

    [Fact]
    public void RiskClassificationMustStayAlignedWithTheMutationRegistry()
    {
        foreach (var tool in Enum.GetValues<AiToolName>())
        {
            var isMutating = AiToolSafetyGate.IsMutating(tool);
            var risk = AgentToolRiskClassifier.Classify(tool);

            if (isMutating)
            {
                risk.Should().NotBe(AgentToolRiskLevel.ReadOnly,
                    $"{tool} mutates business data and cannot be classified as read-only");
                AgentToolRiskClassifier.RequiresExplicitApproval(tool).Should().BeTrue(
                    $"{tool} mutates business data and must require explicit approval");
            }
            else
            {
                risk.Should().Be(AgentToolRiskLevel.ReadOnly,
                    $"{tool} is not registered as a mutation and should be explicitly reviewed if it changes data");
            }
        }
    }

    [Theory]
    [InlineData(AiToolName.GetRevenue, AgentToolRiskLevel.ReadOnly)]
    [InlineData(AiToolName.SearchProduct, AgentToolRiskLevel.ReadOnly)]
    [InlineData(AiToolName.CreateTask, AgentToolRiskLevel.Low)]
    [InlineData(AiToolName.CreateCustomer, AgentToolRiskLevel.High)]
    [InlineData(AiToolName.UpdateCompanyProfile, AgentToolRiskLevel.High)]
    [InlineData(AiToolName.CreateSale, AgentToolRiskLevel.Critical)]
    [InlineData(AiToolName.AdjustInventory, AgentToolRiskLevel.Critical)]
    [InlineData(AiToolName.UpdateTaxDefaults, AgentToolRiskLevel.Critical)]
    public void KnownToolsHaveExpectedRiskLevels(AiToolName tool, AgentToolRiskLevel expected)
    {
        AgentToolRiskClassifier.Classify(tool).Should().Be(expected);
    }
}

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
    public void EveryMutatingToolRequiresExplicitApproval()
    {
        foreach (var tool in Enum.GetValues<AiToolName>().Where(AiToolSafetyGate.IsMutating))
        {
            AgentToolRiskClassifier.RequiresExplicitApproval(tool).Should().BeTrue(
                $"{tool} mutates business data and must require explicit approval");
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

using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AgentMutatingToolConfirmationTests
{
    [Fact]
    public void EveryMutatingToolRequiresTheNormalConfirmationFlow()
    {
        var mutatingTools = Enum.GetValues<AiToolName>()
            .Where(AiToolSafetyGate.IsMutating)
            .ToArray();

        mutatingTools.Should().NotBeEmpty();
        foreach (var tool in mutatingTools)
            AgentActionClarifier.NeedsBrief(tool).Should().BeTrue(
                $"{tool} changes business data and must require confirmation");
    }

    [Fact]
    public void OnboardingProfileApplicationIsClassifiedAsMutatingAndRequiresConfirmation()
    {
        AiToolSafetyGate.IsMutating(AiToolName.ApplyOnboardingProfile).Should().BeTrue();
        AgentActionClarifier.NeedsBrief(AiToolName.ApplyOnboardingProfile).Should().BeTrue();
    }
}

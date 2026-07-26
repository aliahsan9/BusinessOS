using System.Text.Json;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AgentSelfCorrectorTests
{
    private readonly AgentSelfCorrector _corrector = new();

    [Fact]
    public void Analyze_Duplicate_SuggestsUpdate()
    {
        var result = _corrector.Analyze(
            AiToolName.CreateCustomer,
            new AiToolResult { ToolName = "CreateCustomer", Success = false, Summary = "Customer already exists." },
            null,
            null,
            "en");

        result.FailureKind.Should().Be(AgentFailureKind.DuplicateEntity);
        result.NeedsClarification.Should().BeTrue();
        result.AlternateTool.Should().Be(AiToolName.UpdateCustomer);
        result.Suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public void Analyze_NotFound_SuggestsSearchRetry()
    {
        var result = _corrector.Analyze(
            AiToolName.UpdateProduct,
            new AiToolResult { ToolName = "UpdateProduct", Success = false, Summary = "Product not found." },
            null,
            null,
            "en");

        result.FailureKind.Should().Be(AgentFailureKind.NotFound);
        result.ShouldRetry.Should().BeTrue();
        result.AlternateTool.Should().Be(AiToolName.SearchProduct);
    }

    [Fact]
    public void Analyze_MissingFields_UrduClarification()
    {
        using var doc = JsonDocument.Parse("""{"quantity":5}""");
        var result = _corrector.Analyze(
            AiToolName.AdjustInventory,
            new AiToolResult { ToolName = "AdjustInventory", Success = false, Summary = "Product is required." },
            null,
            doc.RootElement,
            "ur");

        result.FailureKind.Should().Be(AgentFailureKind.ValidationMissingFields);
        result.NeedsClarification.Should().BeTrue();
        result.ClarificationMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Analyze_Permission_DeniesClearly()
    {
        var result = _corrector.Analyze(
            AiToolName.CreateCustomer,
            new AiToolResult { ToolName = "CreateCustomer", Success = false, Summary = "Permission denied." },
            null,
            null,
            "en");

        result.FailureKind.Should().Be(AgentFailureKind.PermissionDenied);
    }
}

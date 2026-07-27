using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Agents;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AgentPlannerTests
{
    private readonly AgentPlanner _planner = new();

    [Fact]
    public void RequiresWorkflow_CustomerThenInvoice_IsTrue()
    {
        var required = _planner.RequiresWorkflow(
            AiCopilotIntent.ActionCreate,
            "Create customer Ahmed then create invoice for him",
            new AiMemoryStateDto());

        required.Should().BeTrue();
    }

    [Fact]
    public void Plan_CustomerThenInvoice_HasOrderedSteps()
    {
        var plan = _planner.Plan(
            "sophia",
            AiCopilotIntent.Workflow,
            "Create customer Ahmed then create invoice for him",
            new AiPageContextDto(),
            new AiMemoryStateDto());

        plan.Steps.Should().HaveCountGreaterThanOrEqualTo(3);
        plan.Steps.Select(s => s.ToolName).Should().Contain(AiToolName.CreateCustomer);
        plan.Steps.Select(s => s.ToolName).Should().Contain(AiToolName.CreateInvoice);
        plan.Steps.OrderBy(s => s.SortOrder).First().ToolName.Should().Be(AiToolName.CreateCustomer);
    }

    [Fact]
    public void Plan_PurchaseOrder_IncludesLowStockAndDraftPo()
    {
        var plan = _planner.Plan(
            "adam",
            AiCopilotIntent.ActionCreate,
            "Create a purchase order for reorder items",
            new AiPageContextDto(),
            new AiMemoryStateDto());

        plan.Steps.Select(s => s.ToolName).Should().Contain(AiToolName.GetLowStock);
        plan.Steps.Select(s => s.ToolName).Should().Contain(AiToolName.CreatePurchaseOrderDraft);
    }

    [Fact]
    public void Plan_PurchaseOrderWithNamedProduct_CreatesDirectly()
    {
        var plan = _planner.Plan(
            "sophia",
            AiCopilotIntent.ActionCreate,
            "Create purchase order for Laptop",
            new AiPageContextDto(),
            new AiMemoryStateDto());

        plan.Steps.Select(s => s.ToolName).Should().Contain(AiToolName.CreatePurchaseOrder);
        plan.Steps.Select(s => s.ToolName).Should().NotContain(AiToolName.CreatePurchaseOrderDraft);
    }

    [Fact]
    public void PlanOnboarding_UsesEnglishTitles()
    {
        var plan = _planner.PlanOnboarding("sophia", "en");

        plan.Title.Should().Be("Business onboarding");
        plan.Steps.Should().Contain(s => s.ToolName == AiToolName.ApplyOnboardingProfile);
    }
}

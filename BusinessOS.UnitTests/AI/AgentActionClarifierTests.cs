using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AgentActionClarifierTests
{
    private static AgentExecutionState State => new();
    private static AiPageContextDto Page => new();

    [Fact]
    public void CreateCustomer_VagueRequest_AsksForDetailsAndDoesNotExecute()
    {
        var decision = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "create a new customer",
            pending: null,
            State,
            Page);

        decision.ShouldExecute.Should().BeFalse();
        decision.Question.Should().Contain("name");
        decision.Question.Should().Contain("phone");
        decision.Question.Should().Contain("email");
        decision.PendingJson.Should().NotBeNullOrWhiteSpace();
        decision.Args.Should().BeNull();
    }

    [Fact]
    public void CreateCustomer_FullDetails_AsksForConfirmation()
    {
        var decision = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "Create customer. His name is Ahmed Ali. Phone number 03001234567. Address Lahore. Email ahmed@gmail.com",
            pending: null,
            State,
            Page);

        decision.ShouldExecute.Should().BeFalse();
        decision.Question.Should().Contain("Ahmed");
        decision.Question.Should().Contain("Should I create this customer?");
        var pending = AgentActionClarifier.Read(decision.PendingJson);
        pending.Should().NotBeNull();
        pending!.AwaitingConfirmation.Should().BeTrue();
    }

    [Fact]
    public void CreateCustomer_FollowUpThenConfirm_ExecutesWithCollectedDetails()
    {
        var first = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "create a new customer",
            pending: null,
            State,
            Page);
        var pending = AgentActionClarifier.Read(first.PendingJson);

        var second = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "Ahmed Ali, 03001234567, ahmed@gmail.com, Lahore",
            pending,
            State,
            Page);

        second.ShouldExecute.Should().BeFalse();
        second.Question.Should().Contain("Ahmed");
        second.Question.Should().Contain("ahmed@gmail.com");

        var confirmed = AgentActionClarifier.Read(second.PendingJson);
        var third = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "Yes, create it",
            confirmed,
            State,
            Page);

        third.ShouldExecute.Should().BeTrue();
        third.Args.Should().NotBeNull();
        third.Args!.Value.GetProperty("email").GetString().Should().Be("ahmed@gmail.com");
        third.Args.Value.GetProperty("firstName").GetString().Should().Be("Ahmed");
        third.Args.Value.GetProperty("phone").GetString().Should().Contain("03001234567");
        third.Args.Value.GetProperty("country").GetString().Should().Be("Pakistan");
    }

    [Fact]
    public void CreateCustomer_Cancel_ClearsTheDraft()
    {
        var first = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "create a new customer",
            pending: null,
            State,
            Page);

        var decision = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "Cancel",
            AgentActionClarifier.Read(first.PendingJson),
            State,
            Page);

        decision.ShouldExecute.Should().BeFalse();
        decision.PendingJson.Should().BeNull();
        decision.Question.Should().Contain("won't");
    }

    [Fact]
    public void CreateSale_VagueRequest_AsksForCustomerProductAndQuantity()
    {
        var decision = AgentActionClarifier.Decide(
            AiToolName.CreateSale,
            "create a new order",
            pending: null,
            State,
            Page);

        decision.ShouldExecute.Should().BeFalse();
        decision.Question.Should().Contain("product");
        decision.Question.Should().Contain("how many");
        decision.Args.Should().BeNull();
    }

    [Fact]
    public void CreateSale_CompleteDetails_ConfirmsBeforeCreating()
    {
        var decision = AgentActionClarifier.Decide(
            AiToolName.CreateSale,
            "Create order for Ahmed Ali, Laptop, quantity 2",
            pending: null,
            State,
            Page);

        decision.ShouldExecute.Should().BeFalse();
        decision.Question.Should().Contain("Ahmed");
        decision.Question.Should().Contain("Laptop");
        decision.Question.Should().Contain("2");
    }

    [Fact]
    public void CreateProduct_WithoutPrice_AsksForPrices()
    {
        var decision = AgentActionClarifier.Decide(
            AiToolName.CreateProduct,
            "create product Wireless Mouse",
            pending: null,
            State,
            Page);

        decision.ShouldExecute.Should().BeFalse();
        decision.Question.Should().Contain("price");
    }

    [Fact]
    public void DeleteCustomer_NamedTarget_ConfirmsBeforeDeleting()
    {
        var decision = AgentActionClarifier.Decide(
            AiToolName.DeleteCustomer,
            "delete customer Ahmed Ali",
            pending: null,
            State,
            Page);

        decision.ShouldExecute.Should().BeFalse();
        decision.Question.Should().Contain("Delete");
        decision.Question.Should().Contain("Ahmed");
    }

    [Fact]
    public void IsDifferentCommand_NewOrder_AbandonsCustomerDraft()
    {
        AgentActionClarifier.IsDifferentCommand("create a new order", AiToolName.CreateCustomer)
            .Should().BeTrue();
        AgentActionClarifier.IsDifferentCommand("03001234567", AiToolName.CreateCustomer)
            .Should().BeFalse();
        AgentActionClarifier.IsSmallTalk("hello").Should().BeTrue();
    }
}

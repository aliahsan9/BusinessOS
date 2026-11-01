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

    [Fact]
    public void CreateCustomer_NameThenPhoneAndAddress_RemembersTheDraft()
    {
        var first = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "Create customer named Asim",
            pending: null,
            State,
            Page);

        first.ShouldExecute.Should().BeFalse();
        first.Question.Should().Contain("Asim");
        first.Question.Should().Contain("phone number");

        var second = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "03291327355 and address Lahore",
            AgentActionClarifier.Read(first.PendingJson),
            State,
            Page);

        second.ShouldExecute.Should().BeFalse();
        second.Question.Should().Contain("email");
        var pending = AgentActionClarifier.Read(second.PendingJson);
        pending!.Slots["phone"].Should().Contain("03291327355");
        pending.Slots["address"].Should().Contain("Lahore");
        pending.AwaitingConfirmation.Should().BeFalse();

        var third = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "asim@example.com",
            pending,
            State,
            Page);

        third.ShouldExecute.Should().BeFalse();
        third.Question.Should().Contain("Should I create this customer?");

        var confirmed = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "Yes, create it",
            AgentActionClarifier.Read(third.PendingJson),
            State,
            Page);

        confirmed.ShouldExecute.Should().BeTrue();
        confirmed.Args!.Value.GetProperty("phone").GetString().Should().Contain("03291327355");
        confirmed.Args.Value.GetProperty("email").GetString().Should().Be("asim@example.com");
    }

    [Fact]
    public void CreateCustomer_Correction_ReplacesThePhone()
    {
        var first = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "Create customer. His name is Ahmed Ali. Phone number 03001234567. Address Lahore. Email ahmed@gmail.com",
            pending: null,
            State,
            Page);

        var corrected = AgentActionClarifier.Decide(
            AiToolName.CreateCustomer,
            "change the phone to 03009998877",
            AgentActionClarifier.Read(first.PendingJson),
            State,
            Page);

        corrected.ShouldExecute.Should().BeFalse();
        var pending = AgentActionClarifier.Read(corrected.PendingJson);
        pending!.Slots["phone"].Should().Be("03009998877");
        pending.Slots["email"].Should().Be("ahmed@gmail.com");
    }

    [Fact]
    public void PendingAction_ExpiresAfterTheLifetime()
    {
        var pending = new AgentPendingAction
        {
            Tool = AiToolName.CreateCustomer,
            UpdatedAtUtc = DateTime.UtcNow.AddHours(-3)
        };

        AgentPendingAction.IsExpired(pending, DateTime.UtcNow).Should().BeTrue();
        AgentPendingAction.IsExpired(
            new AgentPendingAction { Tool = AiToolName.CreateCustomer, UpdatedAtUtc = DateTime.UtcNow },
            DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Cancel_IsNotAProductSearch()
    {
        AgentActionClarifier.IsCancel("Cancel").Should().BeTrue();
        AgentActionClarifier.IsCancel("stop that").Should().BeTrue();
        var intent = new BusinessOS.Infrastructure.AI.Copilot.AiIntentDetector()
            .Detect("Cancel", new AiPageContextDto(), new AiMemoryStateDto());
        intent.SuggestedTools.Should().NotContain(AiToolName.GetProducts);
        intent.SuggestedTools.Should().NotContain(AiToolName.SearchProduct);
    }

    [Theory]
    [InlineData("Cancel")]
    [InlineData("Don't create")]
    [InlineData("do not create")]
    public void IsCancel_StopsADraft(string message)
    {
        AgentActionClarifier.IsCancel(message).Should().BeTrue();
    }

    [Theory]
    [InlineData("Who are you")]
    [InlineData("top product sold")]
    [InlineData("switch to urdu language")]
    [InlineData("kon sa product ziada sale ho rha hai")]
    [InlineData("Change your accent")]
    public void ShouldReleaseDraft_LetsANewQuestionThrough(string message)
    {
        AgentActionClarifier.ShouldReleaseDraft(message).Should().BeTrue();
    }

    [Fact]
    public void SaleSentence_KeepsCustomerProductAndQuantity()
    {
        const string message = "create new customer with name is David and sale 2 laptops to him, with 50000 each price";

        SaleOrderLanguage.ExtractCustomerName(message).Should().Be("David");
        SaleOrderLanguage.ExtractProductPhrase(message).Should().Be("laptops");
        SaleOrderLanguage.TryParseQuantity(message, out var quantity).Should().BeTrue();
        quantity.Should().Be(2);
    }
}

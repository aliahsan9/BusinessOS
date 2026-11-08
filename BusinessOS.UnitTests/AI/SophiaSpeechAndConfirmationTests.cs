using System.Text.Json;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using BusinessOS.Infrastructure.AI.Agents.Tools;
using BusinessOS.Infrastructure.AI.Copilot;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class SophiaSpeechAndConfirmationTests
{
    private readonly AiIntentDetector _detector = new();
    private static AiPageContextDto Page => new();
    private static AiMemoryStateDto Memory => new();
    private static AgentExecutionState State => new();

    [Theory]
    [InlineData("How can I improve my inventory management?")]
    [InlineData("How can I improve my products?")]
    [InlineData("We need more employees.")]
    [InlineData("I need another employee")]
    [InlineData("What's new with my products?")]
    public void GeneralLanguage_DoesNotSelectAWriteTool(string message)
    {
        var result = _detector.Detect(message, Page, Memory);

        result.Intent.Should().NotBe(AiCopilotIntent.ActionCreate);
        result.SuggestedTools.Should().NotContain(AiToolName.CreateProduct);
        result.SuggestedTools.Should().NotContain(AiToolName.CreateCustomer);
        result.SuggestedTools.Should().NotContain(AiToolName.CreateTask);
        SophiaSpeechActs.AllowsDatabaseWrite(message).Should().BeFalse();
    }

    [Fact]
    public void ShowMyProducts_IsARead_NotACreate()
    {
        var result = _detector.Detect("Show me my products.", Page, Memory);

        result.Intent.Should().NotBe(AiCopilotIntent.ActionCreate);
        result.SuggestedTools.Should().Contain(AiToolName.GetProducts);
        result.SuggestedTools.Should().NotContain(AiToolName.CreateProduct);
    }

    [Fact]
    public void ShowMyCustomers_DoesNotCreateACustomer()
    {
        var result = _detector.Detect("Show my customers", Page, Memory);

        result.SuggestedTools.Should().NotContain(AiToolName.CreateCustomer);
        result.Intent.Should().NotBe(AiCopilotIntent.ActionCreate);
    }

    [Fact]
    public void ExplicitProductCreate_IsAnActionAndDoesNotRunYet()
    {
        var result = _detector.Detect(
            "Create a product named Dell Laptop with SKU DELL-001, cost price 150000, sale price 170000.",
            Page,
            Memory);

        result.Intent.Should().Be(AiCopilotIntent.ActionCreate);
        result.SuggestedTools.Should().Contain(AiToolName.CreateProduct);

        var decision = AgentActionClarifier.Decide(AiToolName.CreateProduct, result.SuggestedTools.Count > 0
            ? "Create a product named Dell Laptop with SKU DELL-001, cost price 150000, sale price 170000."
            : "", null, State, Page);

        decision.ShouldExecute.Should().BeFalse();
        decision.Question.Should().Contain("Dell Laptop");
        decision.Question.Should().Contain("DELL-001");
        decision.Question.Should().Contain("150,000");
        decision.Question.Should().Contain("170,000");
        decision.Question.Should().NotContain("generate a SKU");
        decision.Confirmation.Should().NotBeNull();
        decision.Confirmation!.Action.Should().Be("Create product");
        decision.Confirmation.Fields.Should().Contain(field => field.Label == "SKU" && field.Value == "DELL-001");
    }

    [Fact]
    public void AddAProduct_AsksForMissingFields_AndDoesNotInventThem()
    {
        var decision = AgentActionClarifier.Decide(AiToolName.CreateProduct, "Add a product", null, State, Page);

        decision.ShouldExecute.Should().BeFalse();
        decision.Args.Should().BeNull();
        decision.Question.Should().Contain("SKU");
        decision.Question.Should().Contain("won't invent");
        decision.Confirmation.Should().BeNull();
    }

    [Fact]
    public void Confirm_ExecutesOnlyThePresentedProduct()
    {
        var prepared = AgentActionClarifier.Decide(
            AiToolName.CreateProduct,
            "Create a product named Dell Laptop with SKU DELL-001, cost price 150000, sale price 170000.",
            null,
            State,
            Page);
        var pending = AgentActionClarifier.Read(prepared.PendingJson);

        var confirmed = AgentActionClarifier.Decide(
            AiToolName.CreateProduct,
            "Yes, create the product",
            pending,
            State,
            Page);

        confirmed.ShouldExecute.Should().BeTrue();
        confirmed.Args!.Value.GetProperty("name").GetString().Should().Be("Dell Laptop");
        confirmed.Args.Value.GetProperty("sku").GetString().Should().Be("DELL-001");
        confirmed.Args.Value.GetProperty("costPrice").GetDecimal().Should().Be(150000);
        confirmed.Args.Value.GetProperty("salePrice").GetDecimal().Should().Be(170000);
    }

    [Fact]
    public void ChangedPrice_RequiresConfirmationAgain()
    {
        var prepared = AgentActionClarifier.Decide(
            AiToolName.CreateProduct,
            "Create a product named Dell Laptop with SKU DELL-001, cost price 150000, sale price 170000.",
            null,
            State,
            Page);
        var pending = AgentActionClarifier.Read(prepared.PendingJson);

        var changed = AgentActionClarifier.Decide(
            AiToolName.CreateProduct,
            "sale price 200000",
            pending,
            State,
            Page);

        changed.ShouldExecute.Should().BeFalse();
        changed.Question.Should().Contain("200,000");
        var updated = AgentActionClarifier.Read(changed.PendingJson);
        updated!.ConfirmationFingerprint.Should().NotBe(pending!.ConfirmationFingerprint);

        var confirmed = AgentActionClarifier.Decide(
            AiToolName.CreateProduct,
            "Confirm",
            updated,
            State,
            Page);

        confirmed.ShouldExecute.Should().BeTrue();
        confirmed.Args!.Value.GetProperty("salePrice").GetDecimal().Should().Be(200000);
        confirmed.Args.Value.GetProperty("sku").GetString().Should().Be("DELL-001");
    }

    [Fact]
    public void Cancel_DropsThePendingProduct()
    {
        var prepared = AgentActionClarifier.Decide(
            AiToolName.CreateProduct,
            "Add a new product",
            null,
            State,
            Page);

        var cancelled = AgentActionClarifier.Decide(
            AiToolName.CreateProduct,
            "Cancel",
            AgentActionClarifier.Read(prepared.PendingJson),
            State,
            Page);

        cancelled.ShouldExecute.Should().BeFalse();
        cancelled.PendingJson.Should().BeNull();
    }

    [Fact]
    public void DecliningSales_IsAnalysis_NotAWrite()
    {
        SophiaSpeechActs.Classify("Why are my sales declining?").Should().Be(SophiaSpeechAct.Analysis);
        var result = _detector.Detect("Why are my sales declining?", Page, Memory);
        result.Intent.Should().NotBe(AiCopilotIntent.ActionCreate);
        result.SuggestedTools.Should().NotContain(AiToolName.CreateProduct);
    }

    [Fact]
    public async Task CreateProduct_MissingSku_DoesNotCallTheDatabaseCommand()
    {
        var (context, _, _) = InMemoryDbContextFactory.Create();
        var mediator = new Mock<IMediator>();
        var tool = new CreateProductTool(mediator.Object, context, NullLogger<CreateProductTool>.Instance);

        var result = await tool.ExecuteWithArgsAsync(
            new AiCopilotExecutionContext { Message = "create product" },
            JsonDocument.Parse("""{"name":"Dell Laptop","salePrice":170000,"costPrice":150000}""").RootElement,
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Summary.Should().Contain("SKU");
        result.Summary.Should().Contain("did not create");
        mediator.Verify(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateProduct_DuplicateSku_DoesNotCreateAnotherProduct()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var category = TestDataFactory.CreateCategory(tenantId, "Laptops");
        context.Categories.Add(category);
        context.Products.Add(TestDataFactory.CreateProduct(tenantId, category.Id, "Existing", "DELL-001"));
        await context.SaveChangesAsync();

        var mediator = new Mock<IMediator>();
        var tool = new CreateProductTool(mediator.Object, context, NullLogger<CreateProductTool>.Instance);
        var result = await tool.ExecuteWithArgsAsync(
            new AiCopilotExecutionContext { Message = "create product" },
            JsonDocument.Parse("""{"name":"Dell Laptop","sku":"DELL-001","salePrice":170000,"costPrice":150000}""").RootElement,
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Summary.Should().Contain("DELL-001");
        result.Summary.Should().Contain("did not create");
        mediator.Verify(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateProduct_UsesTheSkuTheUserGave()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        context.Categories.Add(TestDataFactory.CreateCategory(tenantId, "Laptops"));
        await context.SaveChangesAsync();

        CreateProductCommand? captured = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Guid>, CancellationToken>((request, _) => captured = (CreateProductCommand)request)
            .ReturnsAsync(Guid.NewGuid());

        var tool = new CreateProductTool(mediator.Object, context, NullLogger<CreateProductTool>.Instance);
        var result = await tool.ExecuteWithArgsAsync(
            new AiCopilotExecutionContext { Message = "create product" },
            JsonDocument.Parse("""{"name":"Dell Laptop","sku":"DELL-001","salePrice":170000,"costPrice":150000}""").RootElement,
            CancellationToken.None);

        result.Success.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.SKU.Should().Be("DELL-001");
        captured.Name.Should().Be("Dell Laptop");
        captured.CostPrice.Should().Be(150000);
        captured.SalePrice.Should().Be(170000);
    }
}

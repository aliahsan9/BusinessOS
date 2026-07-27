using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AgentArgumentExtractorTests
{
    [Fact]
    public void Heuristic_CreateCustomer_ExtractsNamePhoneEmailAddress()
    {
        var message =
            "Create customer. His name is Ahmed Ali. Phone number 03001234567. Address Lahore. Email ahmed@gmail.com";

        var args = AgentArgumentExtractor.ExtractHeuristic(
            AiToolName.CreateCustomer,
            message,
            new AgentExecutionState(),
            new AiPageContextDto());

        args.Should().NotBeNull();
        args!.Value.GetProperty("firstName").GetString().Should().Be("Ahmed");
        args.Value.GetProperty("lastName").GetString().Should().Contain("Ali");
        args.Value.GetProperty("email").GetString().Should().Be("ahmed@gmail.com");
        args.Value.GetProperty("phone").GetString().Should().Contain("03001234567");
        args.Value.GetProperty("city").GetString().Should().Be("Lahore");
    }

    [Fact]
    public void Heuristic_SearchCustomer_ExtractsQuery()
    {
        var args = AgentArgumentExtractor.ExtractHeuristic(
            AiToolName.SearchCustomer,
            "Find customer Ahmed",
            new AgentExecutionState(),
            new AiPageContextDto());

        args.Should().NotBeNull();
        args!.Value.GetProperty("query").GetString().Should().Contain("Ahmed");
    }

    [Fact]
    public void Heuristic_AdjustInventory_UsesStateProductAndQuantity()
    {
        var state = new AgentExecutionState { ProductId = Guid.NewGuid(), ProductName = "Laptop" };
        var args = AgentArgumentExtractor.ExtractHeuristic(
            AiToolName.AdjustInventory,
            "Adjust inventory by 5 units",
            state,
            new AiPageContextDto());

        args.Should().NotBeNull();
        args!.Value.GetProperty("quantity").GetDecimal().Should().Be(5);
        args.Value.GetProperty("productId").GetString().Should().Be(state.ProductId.ToString());
    }

    [Fact]
    public void Heuristic_CreatePurchaseOrder_ExtractsProductAndQuantity()
    {
        var args = AgentArgumentExtractor.ExtractHeuristic(
            AiToolName.CreatePurchaseOrder,
            "Create purchase order for Laptop quantity 5",
            new AgentExecutionState(),
            new AiPageContextDto());

        args.Should().NotBeNull();
        args!.Value.GetProperty("productName").GetString().Should().Contain("Laptop");
        args.Value.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
        args.Value.GetProperty("quantity").GetDecimal().Should().Be(5);
    }

    [Fact]
    public void Heuristic_CreatePurchaseOrder_ProductOnlyReply()
    {
        var args = AgentArgumentExtractor.ExtractHeuristic(
            AiToolName.CreatePurchaseOrder,
            "Laptop",
            new AgentExecutionState(),
            new AiPageContextDto());

        args.Should().NotBeNull();
        args!.Value.GetProperty("productName").GetString().Should().Be("Laptop");
    }

    [Fact]
    public void Heuristic_CreateSupplier_ExtractsName()
    {
        var args = AgentArgumentExtractor.ExtractHeuristic(
            AiToolName.CreateSupplier,
            "Create supplier Acme Traders phone 03001112233",
            new AgentExecutionState(),
            new AiPageContextDto());

        args.Should().NotBeNull();
        args!.Value.GetProperty("name").GetString().Should().Contain("Acme");
        args.Value.GetProperty("phone").GetString().Should().Contain("03001112233");
    }
}

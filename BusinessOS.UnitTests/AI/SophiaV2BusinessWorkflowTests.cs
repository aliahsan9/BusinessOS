using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Customers.Commands.CreateCustomer;
using BusinessOS.Application.Features.Inventory.Queries;
using BusinessOS.Application.Features.Orders.Commands.CreateOrder;
using BusinessOS.Application.Features.Orders.Queries;
using BusinessOS.Application.Features.Orders.Commands.UpdateOrderStatus;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI.Agents.Tools;
using BusinessOS.Infrastructure.AI.Copilot.Tools;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BusinessOS.UnitTests.AI;

/// <summary>
/// Isolated EF in-memory tenant. This does not open the development or production SQL database.
/// </summary>
public class SophiaV2BusinessWorkflowTests
{
    [Fact]
    public async Task ShopWorkflow_ValidatesCustomer_SellsStock_CancelsAndRestores_AndHidesTheOtherTenant()
    {
        var database = Guid.NewGuid().ToString();
        var (context, tenantId, provider) = TenantDatabase.Open(name: database);
        var (other, otherTenant, _) = TenantDatabase.Open(name: database);

        var invalid = new CreateCustomerCommandValidator().Validate(
            new CreateCustomerCommand("Ahmed", "Khan", "", "03001234567", "Street 1", "Lahore", "Pakistan", "54000"));
        invalid.IsValid.Should().BeFalse();
        (await context.Customers.CountAsync()).Should().Be(0);

        var injectedEmail = new CreateCustomerCommandValidator().Validate(
            new CreateCustomerCommand("Ahmed", "Khan", "ahmed@example.com'; DROP TABLE Customers;--", "03001234567", "Street 1", "Lahore", "Pakistan", "54000"));
        injectedEmail.IsValid.Should().BeFalse();

        var customerId = await Customers(context, provider).Handle(
            new CreateCustomerCommand(
                "Ahmed",
                "O'Brien; DROP TABLE Customers",
                "sophia.workflow@example.com",
                "03001234567",
                "Street 1",
                "Lahore",
                "Pakistan",
                "54000"),
            CancellationToken.None);
        var duplicate = () => Customers(context, provider).Handle(
            new CreateCustomerCommand("Ada", "Lovelace", "sophia.workflow@example.com", "03007654321", "Street 2", "Lahore", "Pakistan", "54000"),
            CancellationToken.None);
        await duplicate.Should().ThrowAsync<ConflictException>();
        (await context.Customers.CountAsync()).Should().Be(1);
        (await context.Customers.SingleAsync()).LastName.Should().Be("O'Brien; DROP TABLE Customers");

        var category = TestDataFactory.CreateCategory(tenantId, "Workflow");
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var firstId = await Products(context, provider).Handle(
            new CreateProductCommand(category.Id, "Workflow Widget", "WF-1", null, 4, 10, 2),
            CancellationToken.None);
        var secondId = await Products(context, provider).Handle(
            new CreateProductCommand(category.Id, "Workflow Lamp", "WF-2", null, 2, 5, 2),
            CancellationToken.None);
        var goneId = await Products(context, provider).Handle(
            new CreateProductCommand(category.Id, "Workflow Gone", "WF-0", null, 1, 3, 1),
            CancellationToken.None);
        (await context.Inventories.SingleAsync(x => x.ProductId == firstId)).CurrentStock.Should().Be(0);

        var inventory = TenantDatabase.Inventory(context);
        await inventory.IncreaseStockAsync(new StockChangeRequest { ProductId = firstId, Quantity = 10 });
        await inventory.IncreaseStockAsync(new StockChangeRequest { ProductId = secondId, Quantity = 4 });

        var orderId = await Orders(context, provider).Handle(
            new CreateOrderCommand(customerId, 0, 0,
            [
                new CreateOrderItemDto(firstId, 2),
                new CreateOrderItemDto(secondId, 3)
            ]),
            CancellationToken.None);

        (await context.Orders.Include(x => x.OrderItems).SingleAsync(x => x.Id == orderId)).OrderItems.Should().HaveCount(2);
        (await context.Orders.SingleAsync(x => x.Id == orderId)).GrandTotal.Should().Be(35m);
        (await context.Inventories.SingleAsync(x => x.ProductId == firstId)).CurrentStock.Should().Be(8);
        (await context.Inventories.SingleAsync(x => x.ProductId == secondId)).CurrentStock.Should().Be(1);

        var oversell = () => Orders(context, provider).Handle(
            new CreateOrderCommand(customerId, 0, 0, [new CreateOrderItemDto(firstId, 9)]),
            CancellationToken.None);
        await oversell.Should().ThrowAsync<BadRequestException>().WithMessage("*Insufficient stock*");
        (await context.Orders.CountAsync()).Should().Be(1);
        (await context.Inventories.SingleAsync(x => x.ProductId == firstId)).CurrentStock.Should().Be(8);

        var low = await new GetLowStockTool(inventory).ExecuteAsync(Execution("which products are low in stock"));
        low.Summary.Should().Contain("Workflow Lamp");
        low.Summary.Should().NotContain("Workflow Gone");

        var beforeCancel = await new GetSalesSummaryTool(context).ExecuteAsync(Execution("show my total sales this month"));
        DecimalField(beforeCancel.Data, "orderTotal").Should().Be(35m);
        beforeCancel.Summary.Should().Contain("not invoiced revenue");

        await Status(context, provider).Handle(new UpdateOrderStatusCommand(orderId, OrderStatusNames.Cancelled), CancellationToken.None);

        (await context.Orders.SingleAsync(x => x.Id == orderId)).Status.Should().Be(OrderStatusNames.Cancelled);
        (await context.Inventories.SingleAsync(x => x.ProductId == firstId)).CurrentStock.Should().Be(10);
        (await context.Inventories.SingleAsync(x => x.ProductId == secondId)).CurrentStock.Should().Be(4);
        DecimalField((await new GetSalesSummaryTool(context).ExecuteAsync(Execution("show my total sales this month"))).Data, "orderTotal")
            .Should().Be(0m);

        var gone = await new GetOutOfStockTool(inventory).ExecuteAsync(Execution("which products are out of stock"));
        gone.Summary.Should().Contain("Workflow Gone");
        gone.Summary.Should().NotContain("Workflow Widget");
        gone.Summary.Should().NotContain("Workflow Lamp");

        (await other.Customers.AnyAsync(x => x.Id == customerId)).Should().BeFalse();
        (await other.Products.AnyAsync(x => x.TenantId == tenantId)).Should().BeFalse();
        (await other.Orders.AnyAsync()).Should().BeFalse();
        otherTenant.Should().NotBe(tenantId);
        _ = goneId;
    }

    private static decimal DecimalField(object? data, string name)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(data));
        return doc.RootElement.GetProperty(name).GetDecimal();
    }

    private static AiCopilotExecutionContext Execution(string message) => new()
    {
        Message = message,
        Intent = AiCopilotIntent.ActionRead,
        Page = new AiPageContextDto(),
        Memory = new AiMemoryStateDto(),
        Request = new AiChatRequest(message),
        SessionId = Guid.NewGuid()
    };

    private static CreateCustomerCommandHandler Customers(BusinessOSDbContext context, ITenantProvider provider)
    {
        var limits = new Mock<ITenantLimitService>();
        limits.Setup(x => x.EnsureWithinLimitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new CreateCustomerCommandHandler(
            context,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            limits.Object,
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<CreateCustomerCommandHandler>());
    }

    private static CreateProductCommandHandler Products(BusinessOSDbContext context, ITenantProvider provider) =>
        new(
            context,
            TenantDatabase.Inventory(context),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<CreateProductCommandHandler>());

    private static CreateOrderCommandHandler Orders(BusinessOSDbContext context, ITenantProvider provider)
    {
        var numbers = new Mock<IOrderNumberGenerator>();
        numbers.Setup(x => x.GenerateNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("ORD-WF-1");
        return new CreateOrderCommandHandler(
            context,
            numbers.Object,
            TenantDatabase.Inventory(context),
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<CreateOrderCommandHandler>());
    }

    private static UpdateOrderStatusCommandHandler Status(BusinessOSDbContext context, ITenantProvider provider) =>
        new(
            context,
            TenantDatabase.Inventory(context),
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateCache(),
            provider,
            TestHandlerDependencies.CreateLogger<UpdateOrderStatusCommandHandler>());
}

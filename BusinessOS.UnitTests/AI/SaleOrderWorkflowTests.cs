using System.Text.Json;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.Orders.Commands.CreateOrder;
using BusinessOS.Application.Features.Orders.Queries;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.AI.Agents.Runtime;
using BusinessOS.Infrastructure.AI.Agents.Tools;
using BusinessOS.Infrastructure.AI.Copilot;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.UnitTests.Common;
using BusinessOS.UnitTests.Handlers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class SaleOrderWorkflowTests
{
    private static AgentExecutionState State => new();
    private static AiPageContextDto Page => new();

    [Fact]
    public void CustomerName_FromFirstMessage_IsKeptOnLaterMessages()
    {
        var first = Decide("Create an order for Amad Ali");
        var pending = AgentActionClarifier.Read(first.PendingJson);

        pending!.Slots["customerName"].Should().Be("Amad Ali");

        var second = Decide("Two HP laptops, generation 5", pending);
        var kept = AgentActionClarifier.Read(second.PendingJson);

        kept!.Slots["customerName"].Should().Be("Amad Ali");
        kept.Slots["productName"].Should().Contain("HP");
        kept.Slots["productName"].Should().Contain("generation");
        kept.Slots["quantity"].Should().Be("2");
        second.Question.Should().NotContain("Which customer is this order for?");
    }

    [Fact]
    public void ProductAndQuantity_SuppliedSeparately_MergeIntoTheDraft()
    {
        var first = Decide("Create an order for Amad Ali");
        var pending = AgentActionClarifier.Read(first.PendingJson);

        var product = Decide("HP laptop generation 5", pending);
        var afterProduct = AgentActionClarifier.Read(product.PendingJson);
        afterProduct!.Slots["customerName"].Should().Be("Amad Ali");
        afterProduct.Slots.Should().NotContainKey("quantity");

        var quantity = Decide("Quantity is 2", afterProduct);
        var draft = AgentActionClarifier.Read(quantity.PendingJson);
        draft!.Slots["customerName"].Should().Be("Amad Ali");
        draft.Slots["productName"].Should().Contain("HP");
        draft.Slots["quantity"].Should().Be("2");
    }

    [Fact]
    public void ResolvedCustomer_IsNotRequestedAgain()
    {
        var first = Decide("Create an order for Amad Ali");
        first.Question.Should().Contain("Amad Ali");
        first.Question.Should().Contain("product");
        first.Question.Should().NotContain("Which customer is this order for?");
    }

    [Fact]
    public void QuantityTwo_IsNotDoubled_AndGenerationIsNotAQuantity()
    {
        SaleOrderLanguage.TryParseQuantity("Two HP laptops, generation 5", out var fromWords).Should().BeTrue();
        fromWords.Should().Be(2);
        SaleOrderLanguage.TryParseQuantity("quantity is 2", out var labeled).Should().BeTrue();
        labeled.Should().Be(2);
        SaleOrderLanguage.TryParseQuantity("Create two HP laptops quantity 2", out var both).Should().BeTrue();
        both.Should().Be(2);
        SaleOrderLanguage.TryParseQuantity("HP generation 5", out _).Should().BeFalse();

        var decision = Decide("Create order for Amad Ali, two HP laptops, generation 5");
        var pending = AgentActionClarifier.Read(decision.PendingJson);
        pending!.Slots["quantity"].Should().Be("2");
        var args = AgentActionClarifier.BuildExecutionArgs(AiToolName.CreateSale, pending.Slots);
        args.GetProperty("items").GetArrayLength().Should().Be(1);
        args.GetProperty("items")[0].GetProperty("quantity").GetDecimal().Should().Be(2);
    }

    [Fact]
    public void Confirmation_YesProceed_ExecutesWithCollectedDetails()
    {
        var started = Decide("Create order for Amad Ali, Laptop, quantity 2");
        var pending = AgentActionClarifier.Read(started.PendingJson);
        pending!.AwaitingConfirmation.Should().BeTrue();

        var confirmed = Decide("Yes, proceed", pending);
        confirmed.ShouldExecute.Should().BeTrue();
        confirmed.Args!.Value.GetProperty("customerName").GetString().Should().Be("Amad Ali");
        confirmed.Args.Value.GetProperty("items")[0].GetProperty("quantity").GetDecimal().Should().Be(2);
    }

    [Fact]
    public async Task UniqueProduct_ResolvesToTheDatabaseId_AndQuotesTheStoredPrice()
    {
        await using var db = CreateContext(out var tenantId);
        var (customer, product) = await SeedLaptopAsync(db, tenantId, "HP Laptop Generation 5", "HP-G5", 1200, 10);

        var started = Decide("Create an order for Amad Ali");
        var withProduct = Decide("Two HP laptops, generation 5", AgentActionClarifier.Read(started.PendingJson));
        var quote = await new SaleOrderDraftService(new SaleOrderCatalog(db))
            .ApplyAsync(withProduct, "Two HP laptops, generation 5", Guid.NewGuid());

        quote.ShouldExecute.Should().BeFalse();
        quote.Question.Should().Contain("Amad Ali");
        quote.Question.Should().Contain("HP Laptop Generation 5");
        quote.Question.Should().Contain("1200.00");
        quote.Question.Should().Contain("2400.00");
        quote.Question.Should().Contain("Should I create this order?");
        var draft = AgentActionClarifier.Read(quote.PendingJson);
        draft!.Slots["customerId"].Should().Be(customer.Id.ToString());
        draft.Slots["productId"].Should().Be(product.Id.ToString());
        draft.Slots["quantity"].Should().Be("2");

        var confirmed = Decide("Yes, proceed", draft);
        var ready = await new SaleOrderDraftService(new SaleOrderCatalog(db))
            .ApplyAsync(confirmed, "Yes, proceed", Guid.NewGuid());
        ready.ShouldExecute.Should().BeTrue();
        ready.Args!.Value.GetProperty("customerId").GetString().Should().Be(customer.Id.ToString());
        ready.Args.Value.GetProperty("items")[0].GetProperty("productId").GetString().Should().Be(product.Id.ToString());
        ready.Args.Value.GetProperty("items")[0].GetProperty("quantity").GetDecimal().Should().Be(2);
    }

    [Fact]
    public async Task AmbiguousCustomerOrProduct_AsksInsteadOfGuessing()
    {
        await using var db = CreateContext(out var tenantId);
        var category = TestDataFactory.CreateCategory(tenantId);
        var first = TestDataFactory.CreateCustomer(tenantId, "amad1@test.com", "Amad", "Ali");
        var second = TestDataFactory.CreateCustomer(tenantId, "amad2@test.com", "Amad", "Ali");
        var gen5A = TestDataFactory.CreateProduct(tenantId, category.Id, "HP Laptop Generation 5", "HP-G5-A", salePrice: 1000, stock: 5);
        var gen5B = TestDataFactory.CreateProduct(tenantId, category.Id, "HP Laptop Generation 5", "HP-G5-B", salePrice: 1100, stock: 5);
        db.AddRange(category, first, second, gen5A, gen5B,
            TestDataFactory.CreateInventory(tenantId, gen5A.Id, 5),
            TestDataFactory.CreateInventory(tenantId, gen5B.Id, 5));
        await db.SaveChangesAsync();

        var customers = Decide("Create an order for Amad Ali");
        var customerDraft = await new SaleOrderDraftService(new SaleOrderCatalog(db))
            .ApplyAsync(customers, "Create an order for Amad Ali", Guid.NewGuid());
        customerDraft.ShouldExecute.Should().BeFalse();
        customerDraft.Question.Should().Contain("more than one matching customer");
        AgentActionClarifier.Read(customerDraft.PendingJson)!.Slots.Should().NotContainKey("customerId");

        var named = Decide("Create order for Sara Khan, HP laptop generation 5, quantity 2");
        var pending = AgentActionClarifier.Read(named.PendingJson)!;
        pending.Slots["customerId"] = first.Id.ToString();
        pending.Slots["customerName"] = "Amad Ali";
        var products = await new SaleOrderDraftService(new SaleOrderCatalog(db))
            .ApplyAsync(AgentActionClarifier.Decide(AiToolName.CreateSale, "HP laptop generation 5", pending, State, Page), "HP laptop generation 5", Guid.NewGuid());
        products.ShouldExecute.Should().BeFalse();
        products.Question.Should().Contain("more than one matching product");
        products.Question.Should().Contain("HP-G5-A");
        products.Question.Should().Contain("HP-G5-B");
        AgentActionClarifier.Read(products.PendingJson)!.Slots.Should().NotContainKey("productId");
    }

    [Fact]
    public async Task MissingProduct_AsksOnlyForTheCatalogGap()
    {
        await using var db = CreateContext(out var tenantId);
        var customer = TestDataFactory.CreateCustomer(tenantId, "amad@test.com", "Amad", "Ali");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var started = Decide("Create an order for Amad Ali");
        var follow = Decide("Two mystery widgets, generation 9", AgentActionClarifier.Read(started.PendingJson));
        var result = await new SaleOrderDraftService(new SaleOrderCatalog(db))
            .ApplyAsync(follow, "Two mystery widgets, generation 9", Guid.NewGuid());

        result.ShouldExecute.Should().BeFalse();
        result.Question.Should().Contain("couldn't find an inventory item");
        result.Question.Should().NotContain("Which customer is this order for?");
        AgentActionClarifier.Read(result.PendingJson)!.Slots["customerId"].Should().Be(customer.Id.ToString());
    }

    [Fact]
    public async Task InsufficientStock_DoesNotCreateTheOrder()
    {
        await using var db = CreateContext(out var tenantId);
        var (customer, product) = await SeedLaptopAsync(db, tenantId, "HP Laptop Generation 5", "HP-G5", 500, 1);
        var tool = CreateTool(db, tenantId, out var mediator);

        var result = await tool.ExecuteWithArgsAsync(Context(Guid.NewGuid()), Args(customer.Id, product.Id, 2), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Summary.Should().Contain("Insufficient stock");
        result.Summary.Should().NotContain("created successfully");
        mediator.Verify(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirmation_InvokesCreateOrder_AndReportsTheRealNumberAndTotal()
    {
        await using var db = CreateContext(out var tenantId);
        var (customer, product) = await SeedLaptopAsync(db, tenantId, "HP Laptop Generation 5", "HP-G5", 1200, 10);
        var numbers = new Mock<IOrderNumberGenerator>();
        numbers.Setup(generator => generator.GenerateNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync("ORD-1001");
        var handler = new CreateOrderCommandHandler(
            db,
            numbers.Object,
            InventoryServiceTestHelper.CreateMock().Object,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateCache(),
            Tenant(tenantId),
            NullLogger<CreateOrderCommandHandler>.Instance);
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .Returns((CreateOrderCommand command, CancellationToken cancellationToken) => handler.Handle(command, cancellationToken));
        var tool = new CreateSaleTool(
            mediator.Object,
            db,
            new SaleOrderCatalog(db),
            new SaleOrderExecutionLedger(),
            Tenant(tenantId),
            NullLogger<CreateSaleTool>.Instance);

        var result = await tool.ExecuteWithArgsAsync(
            Context(Guid.NewGuid()),
            DuplicateLines(customer.Id, product.Id, 2),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Summary.Should().Contain("created successfully");
        result.Summary.Should().Contain("ORD-1001");
        result.Summary.Should().Contain("2400.00");
        var saved = await db.Orders.Include(order => order.OrderItems).SingleAsync();
        saved.OrderNumber.Should().Be("ORD-1001");
        saved.CustomerId.Should().Be(customer.Id);
        saved.GrandTotal.Should().Be(2400);
        saved.OrderItems.Should().ContainSingle();
        saved.OrderItems.Single().Quantity.Should().Be(2);
        saved.OrderItems.Single().ProductId.Should().Be(product.Id);
        mediator.Verify(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FailedCreate_NeverProducesASuccessMessage()
    {
        await using var db = CreateContext(out var tenantId);
        var (customer, product) = await SeedLaptopAsync(db, tenantId, "HP Laptop Generation 5", "HP-G5", 1200, 10);
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Customer is not active."));
        var tool = new CreateSaleTool(
            mediator.Object,
            db,
            new SaleOrderCatalog(db),
            new SaleOrderExecutionLedger(),
            Tenant(tenantId),
            NullLogger<CreateSaleTool>.Instance);

        var result = await tool.ExecuteWithArgsAsync(Context(Guid.NewGuid()), Args(customer.Id, product.Id, 2), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ActionResult.Should().BeNull();
        result.Summary.Should().Contain("not created");
        result.Summary.Should().Contain("not active");
        result.Summary.Should().NotContain("created successfully");
    }

    [Fact]
    public async Task RepeatedConfirmation_DoesNotCreateASecondOrder()
    {
        await using var db = CreateContext(out var tenantId);
        var (customer, product) = await SeedLaptopAsync(db, tenantId, "HP Laptop Generation 5", "HP-G5", 1200, 10);
        var ledger = new SaleOrderExecutionLedger();
        var mediator = new Mock<IMediator>();
        var creates = 0;
        mediator
            .Setup(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateOrderCommand _, CancellationToken _) =>
            {
                creates++;
                var order = TestDataFactory.CreateOrder(tenantId, customer.Id, grandTotal: 2400, orderNumber: "ORD-1001");
                db.Orders.Add(order);
                db.SaveChanges();
                return order.Id;
            });
        var tool = new CreateSaleTool(mediator.Object, db, new SaleOrderCatalog(db), ledger, Tenant(tenantId), NullLogger<CreateSaleTool>.Instance);
        var sessionId = Guid.NewGuid();

        var first = await tool.ExecuteWithArgsAsync(Context(sessionId), Args(customer.Id, product.Id, 2), CancellationToken.None);
        var second = await tool.ExecuteWithArgsAsync(Context(sessionId), Args(customer.Id, product.Id, 2), CancellationToken.None);

        creates.Should().Be(1);
        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        second.Summary.Should().Contain("ORD-1001");
        second.Summary.Should().Contain("already created");
    }

    [Fact]
    public async Task TenantAndConversation_DoNotShareSaleState()
    {
        var database = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var dbA = CreateContext(tenantA, database);
        await using var dbB = CreateContext(tenantB, database);
        var (customerA, productA) = await SeedLaptopAsync(dbA, tenantA, "HP Laptop Generation 5", "HP-G5", 100, 8);
        await SeedLaptopAsync(dbB, tenantB, "Other Laptop", "OTHER", 50, 8, "Sara", "Khan");

        var found = await new SaleOrderCatalog(dbB).ResolveCustomerAsync(customerA.Id.ToString(), "Amad Ali");
        found.Kind.Should().Be(SaleMatchKind.NotFound);

        var ledger = new SaleOrderExecutionLedger();
        var mediator = new Mock<IMediator>();
        var creates = 0;
        mediator
            .Setup(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateOrderCommand command, CancellationToken _) =>
            {
                creates++;
                var order = TestDataFactory.CreateOrder(command.CustomerId == customerA.Id ? tenantA : tenantB, command.CustomerId, grandTotal: 200, orderNumber: "ORD-" + creates);
                (command.CustomerId == customerA.Id ? dbA : dbB).Orders.Add(order);
                (command.CustomerId == customerA.Id ? dbA : dbB).SaveChanges();
                return order.Id;
            });

        var toolA = new CreateSaleTool(mediator.Object, dbA, new SaleOrderCatalog(dbA), ledger, Tenant(tenantA), NullLogger<CreateSaleTool>.Instance);
        var toolB = new CreateSaleTool(mediator.Object, dbB, new SaleOrderCatalog(dbB), ledger, Tenant(tenantB), NullLogger<CreateSaleTool>.Instance);
        var customerB = await dbB.Customers.SingleAsync();
        var productB = await dbB.Products.SingleAsync();

        var left = await toolA.ExecuteWithArgsAsync(Context(Guid.NewGuid()), Args(customerA.Id, productA.Id, 2), CancellationToken.None);
        var right = await toolB.ExecuteWithArgsAsync(
            Context(Guid.NewGuid()),
            Args(customerB.Id, productB.Id, 2, "Sara Khan", "Other Laptop"),
            CancellationToken.None);

        left.Success.Should().BeTrue(left.Summary);
        right.Success.Should().BeTrue(right.Summary);
        left.Summary.Should().NotBe(right.Summary);
        creates.Should().Be(2);
    }

    [Fact]
    public async Task ToolArguments_CannotCrossTenantsOrSkipPermission()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.Roles).Returns([]);
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(false);
        var denied = new AiPermissionValidator(user.Object).ValidateTool(AiToolName.CreateSale);
        denied.Allowed.Should().BeFalse();
        denied.MissingPermissions.Should().Contain(PermissionCodes.OrderCreate);

        await using var db = CreateContext(out var tenantId);
        var (customer, product) = await SeedLaptopAsync(db, tenantId, "HP Laptop Generation 5", "HP-G5", 100, 5);
        var tool = CreateTool(db, tenantId, out var mediator);
        using var document = JsonDocument.Parse($$"""
            {
              "tenantId": "{{Guid.NewGuid()}}",
              "customerId": "{{customer.Id}}",
              "productId": "{{product.Id}}",
              "quantity": 2,
              "items": [{ "productId": "{{product.Id}}", "quantity": 2 }]
            }
            """);

        var result = await tool.ExecuteWithArgsAsync(Context(Guid.NewGuid()), document.RootElement.Clone(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Summary.Should().Contain("another business");
        result.Summary.Should().NotContain("created successfully");
        mediator.Verify(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AgentClarificationDecision Decide(string message, AgentPendingAction? pending = null) =>
        AgentActionClarifier.Decide(AiToolName.CreateSale, message, pending, State, Page);

    private static BusinessOSDbContext CreateContext(out Guid tenantId) =>
        CreateContext(tenantId = Guid.NewGuid(), Guid.NewGuid().ToString());

    private static BusinessOSDbContext CreateContext(Guid tenantId, string database)
    {
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(database)
            .Options;
        return new BusinessOSDbContext(options, provider);
    }

    private static async Task<(Customer Customer, Product Product)> SeedLaptopAsync(
        BusinessOSDbContext db,
        Guid tenantId,
        string productName,
        string sku,
        decimal price,
        decimal stock,
        string firstName = "Amad",
        string lastName = "Ali")
    {
        var category = TestDataFactory.CreateCategory(tenantId);
        var customer = TestDataFactory.CreateCustomer(tenantId, $"{firstName}.{lastName}@test.com", firstName, lastName);
        var product = TestDataFactory.CreateProduct(tenantId, category.Id, productName, sku, salePrice: price, stock: stock);
        db.AddRange(category, customer, product, TestDataFactory.CreateInventory(tenantId, product.Id, stock));
        await db.SaveChangesAsync();
        return (customer, product);
    }

    private static CreateSaleTool CreateTool(
        BusinessOSDbContext db,
        Guid tenantId,
        out Mock<IMediator> mediator,
        Action<CreateOrderCommand>? onCreate = null)
    {
        mediator = new Mock<IMediator>();
        var mock = mediator;
        mediator
            .Setup(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateOrderCommand command, CancellationToken _) =>
            {
                onCreate?.Invoke(command);
                var subtotal = 1200m * command.Items.Sum(item => item.Quantity);
                var order = TestDataFactory.CreateOrder(tenantId, command.CustomerId, grandTotal: subtotal, orderNumber: "ORD-1001");
                db.Orders.Add(order);
                db.SaveChanges();
                return order.Id;
            });
        _ = mock;
        return new CreateSaleTool(
            mediator.Object,
            db,
            new SaleOrderCatalog(db),
            new SaleOrderExecutionLedger(),
            Tenant(tenantId),
            NullLogger<CreateSaleTool>.Instance);
    }

    private static ITenantProvider Tenant(Guid tenantId)
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.Setup(t => t.HasTenant()).Returns(true);
        tenant.Setup(t => t.TenantId).Returns(tenantId);
        return tenant.Object;
    }

    private static AiCopilotExecutionContext Context(Guid sessionId) => new()
    {
        Request = new AiChatRequest("Yes, proceed"),
        Page = new AiPageContextDto(),
        Intent = AiCopilotIntent.ActionCreate,
        SessionId = sessionId,
        Memory = new AiMemoryStateDto(),
        Message = "Yes, proceed"
    };

    private static JsonElement Args(
        Guid customerId,
        Guid productId,
        decimal quantity,
        string customerName = "Amad Ali",
        string productName = "HP Laptop Generation 5")
    {
        using var document = JsonDocument.Parse($$"""
            {
              "customerId": "{{customerId}}",
              "customerName": "{{customerName}}",
              "items": [{ "productId": "{{productId}}", "productName": "{{productName}}", "quantity": {{quantity}} }]
            }
            """);
        return document.RootElement.Clone();
    }

    private static JsonElement DuplicateLines(Guid customerId, Guid productId, decimal quantity)
    {
        using var document = JsonDocument.Parse($$"""
            {
              "customerId": "{{customerId}}",
              "customerName": "Amad Ali",
              "quantity": {{quantity}},
              "items": [
                { "productId": "{{productId}}", "productName": "HP Laptop Generation 5", "quantity": {{quantity}} },
                { "productId": "{{productId}}", "productName": "HP Laptop Generation 5", "quantity": {{quantity}} }
              ]
            }
            """);
        return document.RootElement.Clone();
    }
}

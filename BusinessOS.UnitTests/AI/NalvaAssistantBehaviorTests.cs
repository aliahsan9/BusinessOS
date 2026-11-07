using BusinessOS.Application.Features.Agents;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.AI.Copilot;
using BusinessOS.Infrastructure.AI.Copilot.Tools;
using BusinessOS.UnitTests.Common;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class NalvaAssistantBehaviorTests
{
    private readonly AiIntentDetector _detector = new();
    private static AiPageContextDto Page => new();

    [Theory]
    [InlineData("Hi")]
    [InlineData("Hello")]
    [InlineData("How are you?")]
    [InlineData("Thank you")]
    public void Greetings_DoNotSelectBusinessTools_EvenAfterASalesTurn(string message)
    {
        var result = _detector.Detect(message, Page, SalesHistory());

        result.Intent.Should().Be(AiCopilotIntent.Conversational);
        result.SuggestedTools.Should().BeEmpty();
        AiNaturalReplyBuilder.IsStandaloneSmallTalk(message).Should().BeTrue();
        var reply = AiNaturalReplyBuilder.BuildConversationalReply(message, Page, null, AgentLanguages.English);
        reply.Should().NotContain("units sold");
        reply.Should().NotContain("0 orders");
    }

    [Fact]
    public void EnglishGreeting_DoesNotStayInUrdu()
    {
        ConversationLanguage.Resolve("Hi", AgentLanguages.Urdu).Language.Should().Be(AgentLanguages.English);
    }

    [Theory]
    [InlineData("Show overdue invoices.")]
    [InlineData("کون سی انوائسز اوورڈیو ہیں؟")]
    public void OverdueInvoices_DoNotInheritMonthlySales(string message)
    {
        var result = _detector.Detect(message, Page, SalesHistory());

        result.SuggestedTools.Should().Contain(AiToolName.GetInvoices);
        result.SuggestedTools.Should().NotContain(AiToolName.GetSalesSummary);
        result.SuggestedTools.Should().NotContain(AiToolName.GetRevenue);

        var registry = new AiToolRegistry([new GetInvoicesTool(null!), new GetSalesSummaryTool(null!)]);
        var selected = registry.SelectTools(result, message, Page, SalesHistory());
        selected.Select(t => t.ToolName).Should().Equal(AiToolName.GetInvoices);
    }

    [Theory]
    [InlineData("Which products are low in stock?", AiToolName.GetLowStock)]
    [InlineData("Mere business mein konsi products ka stock kam hai?", AiToolName.GetLowStock)]
    [InlineData("کون سی مصنوعات کا اسٹاک کم ہو رہا ہے؟", AiToolName.GetLowStock)]
    [InlineData("ما هي المنتجات التي أوشكت على النفاد؟", AiToolName.GetLowStock)]
    [InlineData("What is our revenue this month?", AiToolName.GetRevenue)]
    [InlineData("How is the health of my business?", AiToolName.GetBusinessHealth)]
    [InlineData("What should I focus on today?", AiToolName.GetDailyPriorities)]
    public void BusinessQuestions_SelectTheMatchingTool(string message, AiToolName tool)
    {
        var result = _detector.Detect(message, Page, SalesHistory());
        result.SuggestedTools.Should().Contain(tool);
        if (tool != AiToolName.GetRevenue)
            result.SuggestedTools.Should().NotContain(AiToolName.GetSalesSummary);
    }

    [Fact]
    public void ProfitFollowUp_UsesProfit_NotAFreshSalesDump()
    {
        var memory = new AiMemoryStateDto
        {
            LastIntent = nameof(AiCopilotIntent.Analytics),
            LastAnalyticsQuery = "Which products are selling the most?",
            RecentTurns =
            [
                new AiMemoryTurnDto { Role = "user", Content = "Which products are selling the most?", SequenceNumber = 1 },
                new AiMemoryTurnDto { Role = "assistant", Content = "Widget leads.", SequenceNumber = 2 }
            ]
        };

        var result = _detector.Detect("Which one has the highest profit?", Page, memory);

        result.Intent.Should().Be(AiCopilotIntent.FollowUp);
        result.SuggestedTools.Should().Contain(AiToolName.ShowProfit);
        result.SuggestedTools.Should().NotContain(AiToolName.GetSalesSummary);
    }

    [Fact]
    public void ScopedModel_DoesNotExposeSalesTools_ForAnInvoiceQuestion()
    {
        var intent = _detector.Detect("Show overdue invoices.", Page, SalesHistory());
        var allowed = AiToolScope.ForModel(intent);

        allowed.Should().NotBeNull();
        allowed.Should().Contain(AiToolName.GetInvoices);
        allowed.Should().NotContain(AiToolName.GetSalesSummary);
    }

    [Fact]
    public async Task RevenueTool_SeparatesOrdersInvoicesAndPayments()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.Customers.Add(Person(tenantId, customerId));
        context.Orders.Add(Sale(tenantId, orderId, customerId, 5000, now));
        context.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            CustomerId = customerId,
            InvoiceNumber = "INV-SPLIT",
            InvoiceDate = now,
            DueDate = now.AddDays(-3),
            Status = "Sent",
            GrandTotal = 1000,
            OutstandingAmount = 400
        });
        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            CustomerId = customerId,
            Amount = 600,
            PaymentMethod = "Cash",
            PaymentDate = now,
            Status = PaymentStatusNames.Completed,
            Currency = "USD",
            RowVersion = [1]
        });
        await context.SaveChangesAsync();

        var result = await new GetRevenueTool(context).ExecuteAsync(Execution("What is our revenue this month?"));

        DecimalField(result.Data, "revenue").Should().Be(1000m);
        DecimalField(result.Data, "orderTotal").Should().Be(5000m);
        DecimalField(result.Data, "paymentsReceived").Should().Be(600m);
        result.Summary.Should().Contain("Invoiced revenue");
        result.Summary.Should().Contain("Order value");
        result.Summary.Should().Contain("Payments collected");
        result.Summary.Should().NotContain("profit is");
    }

    [Fact]
    public async Task InventoryHealth_UsesReorderLevel_AndDoesNotInventProducts()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var categoryId = Guid.NewGuid();
        var lowId = Guid.NewGuid();
        context.Categories.Add(new Category { Id = categoryId, TenantId = tenantId, Name = "Parts" });
        context.Products.Add(new Product
        {
            Id = lowId,
            TenantId = tenantId,
            CategoryId = categoryId,
            Name = "Copper Wire",
            SKU = "CW-1",
            CostPrice = 10,
            SalePrice = 20,
            IsActive = true,
            RowVersion = [1]
        });
        context.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = lowId,
            CurrentStock = 2,
            ReorderLevel = 5,
            MinimumStockLevel = 5,
            LastUpdated = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await new GetInventoryHealthTool(context).ExecuteAsync(Execution("What is my inventory value?"));

        result.Summary.Should().Contain("Copper Wire");
        result.Summary.Should().Contain("CW-1");
        result.Summary.Should().Contain("Replenish");
        result.Summary.Should().NotContain("Laptop");
    }

    [Fact]
    public async Task InventoryHealth_DoesNotReadAnotherTenant()
    {
        var database = Guid.NewGuid().ToString();
        var (ours, ourTenant, _) = TenantDatabase.Open(name: database);
        var (theirs, theirTenant, _) = TenantDatabase.Open(name: database);
        var categoryId = Guid.NewGuid();
        var ourProduct = Guid.NewGuid();
        var theirProduct = Guid.NewGuid();
        ours.Categories.Add(new Category { Id = categoryId, TenantId = ourTenant, Name = "Ours" });
        theirs.Categories.Add(new Category { Id = Guid.NewGuid(), TenantId = theirTenant, Name = "Theirs" });
        ours.Products.Add(Product(ourTenant, categoryId, ourProduct, "Our Widget", "OUR-1"));
        theirs.Products.Add(Product(theirTenant, Guid.NewGuid(), theirProduct, "Secret Widget", "SEC-1"));
        ours.Inventories.Add(Stock(ourTenant, ourProduct, 1, 4));
        theirs.Inventories.Add(Stock(theirTenant, theirProduct, 1, 4));
        await ours.SaveChangesAsync();
        await theirs.SaveChangesAsync();

        var result = await new GetInventoryHealthTool(ours).ExecuteAsync(Execution("inventory value"));

        result.Summary.Should().Contain("Our Widget");
        result.Summary.Should().NotContain("Secret Widget");
    }

    private static AiMemoryStateDto SalesHistory() => new()
    {
        LastIntent = nameof(AiCopilotIntent.Analytics),
        LastAnalyticsQuery = "What are my sales this month?",
        RecentTurns =
        [
            new AiMemoryTurnDto { Role = "user", Content = "What are my sales this month?", SequenceNumber = 1 },
            new AiMemoryTurnDto { Role = "assistant", Content = "0 units sold across 0 orders this month.", SequenceNumber = 2 }
        ]
    };

    private static AiCopilotExecutionContext Execution(string message) => new()
    {
        Message = message,
        Intent = AiCopilotIntent.ActionRead,
        Page = Page,
        Memory = new AiMemoryStateDto(),
        Request = new AiChatRequest(message),
        SessionId = Guid.NewGuid()
    };

    private static decimal DecimalField(object? data, string name)
    {
        data.Should().NotBeNull();
        var value = data!.GetType().GetProperty(name)?.GetValue(data);
        value.Should().NotBeNull();
        return Convert.ToDecimal(value);
    }

    private static Customer Person(Guid tenantId, Guid id) => new()
    {
        Id = id,
        TenantId = tenantId,
        FirstName = "Ada",
        LastName = "Buyer",
        Email = "ada@example.com",
        PhoneNumber = "03000000000",
        Address = "Street",
        City = "Lahore",
        Country = "Pakistan",
        PostalCode = "54000"
    };

    private static Order Sale(Guid tenantId, Guid id, Guid customerId, decimal total, DateTime when) => new()
    {
        Id = id,
        TenantId = tenantId,
        CustomerId = customerId,
        OrderNumber = "SO-1",
        OrderDate = when,
        Status = OrderStatusNames.Completed,
        GrandTotal = total
    };

    private static Product Product(Guid tenantId, Guid categoryId, Guid id, string name, string sku) => new()
    {
        Id = id,
        TenantId = tenantId,
        CategoryId = categoryId,
        Name = name,
        SKU = sku,
        CostPrice = 8,
        SalePrice = 15,
        IsActive = true,
        RowVersion = [1]
    };

    private static Inventory Stock(Guid tenantId, Guid productId, decimal onHand, decimal reorder) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ProductId = productId,
        CurrentStock = onHand,
        ReorderLevel = reorder,
        MinimumStockLevel = reorder,
        LastUpdated = DateTime.UtcNow
    };
}

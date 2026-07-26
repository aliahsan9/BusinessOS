using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.AI.Copilot.Tools;
using BusinessOS.UnitTests.Common;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class AiAnalyticsToolTests
{
    [Fact]
    public async Task GetBestSellingProductsTool_RanksByUnitsSold()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var now = DateTime.UtcNow;
        var customerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var laptopId = Guid.NewGuid();
        var mouseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        context.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Electronics"
        });
        context.Products.AddRange(
            new Product
            {
                Id = laptopId,
                TenantId = tenantId,
                CategoryId = categoryId,
                Name = "Laptop Pro",
                SKU = "LP-1",
                CostPrice = 500,
                SalePrice = 1000
            },
            new Product
            {
                Id = mouseId,
                TenantId = tenantId,
                CategoryId = categoryId,
                Name = "Wireless Mouse",
                SKU = "WM-1",
                CostPrice = 10,
                SalePrice = 25
            });
        context.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            PhoneNumber = "111",
            Address = "1 Street",
            City = "London",
            Country = "UK",
            PostalCode = "E1"
        });
        context.Orders.Add(new Order
        {
            Id = orderId,
            TenantId = tenantId,
            CustomerId = customerId,
            OrderNumber = "ORD-1",
            OrderDate = now,
            GrandTotal = 3075
        });
        context.OrderItems.AddRange(
            new OrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                ProductId = laptopId,
                Quantity = 3,
                UnitPrice = 1000,
                Total = 3000
            },
            new OrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = orderId,
                ProductId = mouseId,
                Quantity = 3,
                UnitPrice = 25,
                Total = 75
            });
        await context.SaveChangesAsync();

        var tool = new GetBestSellingProductsTool(context);
        var result = await tool.ExecuteAsync(new AiCopilotExecutionContext
        {
            Message = "Which products are best selling this month?",
            Intent = AiCopilotIntent.Analytics,
            Page = new AiPageContextDto(),
            Memory = new AiMemoryStateDto(),
            Request = new AiChatRequest("Which products are best selling this month?"),
            SessionId = Guid.NewGuid()
        });

        result.Success.Should().BeTrue();
        result.Summary.Should().Contain("Laptop Pro");
        result.Summary.Should().Contain("Wireless Mouse");
        result.Summary.IndexOf("Laptop Pro", StringComparison.Ordinal)
            .Should().BeLessThan(result.Summary.IndexOf("Wireless Mouse", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetBestSellingProductsTool_EmptyPeriod_DoesNotInventProducts()
    {
        var (context, _, _) = InMemoryDbContextFactory.Create();
        var tool = new GetBestSellingProductsTool(context);

        var result = await tool.ExecuteAsync(new AiCopilotExecutionContext
        {
            Message = "best selling products this month",
            Intent = AiCopilotIntent.Analytics,
            Page = new AiPageContextDto(),
            Memory = new AiMemoryStateDto(),
            Request = new AiChatRequest("best selling products this month"),
            SessionId = Guid.NewGuid()
        });

        result.Summary.Should().Contain("No product sales found");
        result.Summary.Should().NotContain("1.");
    }

    [Fact]
    public async Task GetSalesTrendsTool_ReturnsPeriodComparison()
    {
        var (context, tenantId, _) = InMemoryDbContextFactory.Create();
        var now = DateTime.UtcNow;
        var thisMonth = new DateTime(now.Year, now.Month, 5, 0, 0, 0, DateTimeKind.Utc);
        var lastMonth = thisMonth.AddMonths(-1);
        var customerId = Guid.NewGuid();

        context.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            FirstName = "Grace",
            LastName = "Hopper",
            Email = "grace@example.com",
            PhoneNumber = "222",
            Address = "2 Street",
            City = "NYC",
            Country = "US",
            PostalCode = "10001"
        });
        context.Orders.AddRange(
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = customerId,
                OrderNumber = "ORD-A",
                OrderDate = thisMonth,
                GrandTotal = 500
            },
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = customerId,
                OrderNumber = "ORD-B",
                OrderDate = lastMonth,
                GrandTotal = 200
            });
        await context.SaveChangesAsync();

        var tool = new GetSalesTrendsTool(context);
        var result = await tool.ExecuteAsync(new AiCopilotExecutionContext
        {
            Message = "What are our sales trends?",
            Intent = AiCopilotIntent.Analytics,
            Page = new AiPageContextDto(),
            Memory = new AiMemoryStateDto(),
            Request = new AiChatRequest("What are our sales trends?"),
            SessionId = Guid.NewGuid()
        });

        result.Summary.Should().Contain("Sales trends");
        result.Summary.Should().Contain("This month so far");
        result.Summary.Should().Contain("Last month");
        result.Summary.Should().Contain("directional");
    }
}

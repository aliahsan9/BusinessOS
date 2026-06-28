using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Categories.Queries.GetAllCategories;
using BusinessOS.Application.Features.Orders.Queries.GetAllOrders;
using BusinessOS.Application.Features.Products.Queries.GetAllProducts;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BusinessOS.UnitTests.Handlers;

public class QueryHandlerTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly BusinessOSDbContext _context;
    private readonly TenantProvider _tenantProvider = new();

    public QueryHandlerTests()
    {
        _tenantProvider.SetTenantId(_tenantId);

        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new BusinessOSDbContext(options, _tenantProvider);
        SeedData();
    }

    [Fact]
    public async Task GetAllCategoriesQueryHandler_ReturnsPagedFilteredResults()
    {
        var handler = new GetAllCategoriesQueryHandler(
            _context,
            Mock.Of<ILogger<GetAllCategoriesQueryHandler>>());

        var result = await handler.Handle(
            new GetAllCategoriesQuery(Search: "Book", Page: 1, PageSize: 10, SortBy: "name"),
            CancellationToken.None);

        result.Items.Should().ContainSingle(x => x.Name == "Books");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllProductsQueryHandler_ReturnsPagedSortedResults()
    {
        var handler = new GetAllProductsQueryHandler(
            _context,
            Mock.Of<ILogger<GetAllProductsQueryHandler>>());

        var result = await handler.Handle(
            new GetAllProductsQuery(
                Search: "SKU",
                Page: 1,
                PageSize: 10,
                SortBy: "name",
                SortDirection: SortDirection.Desc),
            CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.First().Name.Should().Be("Monitor");
    }

    [Fact]
    public async Task GetAllOrdersQueryHandler_ReturnsPagedFilteredResults()
    {
        var customerId = Guid.NewGuid();
        _context.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = _tenantId,
            Name = "Ali Customer",
            Email = "ali@test.com",
            Phone = "123",
            Address = "Street"
        });

        _context.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CustomerId = customerId,
            OrderNumber = "ORD-2026-000001",
            Status = OrderStatusNames.Pending,
            GrandTotal = 100,
            TotalAmount = 100
        });

        _context.SaveChanges();

        var handler = new GetAllOrdersQueryHandler(
            _context,
            Mock.Of<ILogger<GetAllOrdersQueryHandler>>());

        var result = await handler.Handle(
            new GetAllOrdersQuery(Search: "Ali", Status: OrderStatusNames.Pending, Page: 1, PageSize: 10),
            CancellationToken.None);

        result.Items.Should().ContainSingle(x => x.CustomerName == "Ali Customer");
        result.TotalCount.Should().Be(1);
    }

    public void Dispose() => _context.Dispose();

    private void SeedData()
    {
        var categoryId = Guid.NewGuid();

        _context.Categories.AddRange(
            new Category
            {
                Id = categoryId,
                TenantId = _tenantId,
                Name = "Books",
                Description = "Reading"
            },
            new Category
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Name = "Games",
                Description = "Fun"
            });

        _context.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                CategoryId = categoryId,
                Name = "Keyboard",
                SKU = "SKU-KEY",
                CostPrice = 10,
                SalePrice = 20,
                ReorderLevel = 1
            },
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                CategoryId = categoryId,
                Name = "Monitor",
                SKU = "SKU-MON",
                CostPrice = 100,
                SalePrice = 200,
                ReorderLevel = 2
            });

        _context.SaveChanges();
    }
}

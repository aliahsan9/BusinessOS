using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Customers.Commands.CreateCustomer;
using BusinessOS.Application.Features.Customers.Commands.DeleteCustomer;
using BusinessOS.Application.Features.Customers.Commands.UpdateCustomer;
using BusinessOS.Application.Features.Customers.Queries.GetCustomerAnalytics;
using BusinessOS.Application.Features.Customers.Queries.GetCustomerById;
using BusinessOS.Application.Features.Customers.Queries.GetCustomerOrders;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.UnitTests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BusinessOS.UnitTests.Handlers;

public class CreateCustomerHandlerTests
{
    [Fact]
    public async Task Handle_WithDuplicateEmail_ThrowsConflictException()
    {
        var customers = new List<Customer>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Email = "ali@test.com",
                FirstName = "Ali",
                LastName = "Ahsan",
                PhoneNumber = "123",
                Address = "Street",
                City = "Lahore",
                Country = "Pakistan",
                PostalCode = "54000",
                TenantId = Guid.NewGuid()
            }
        };

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(customers.AsQueryable()).Object);

        var limitService = new Mock<ITenantLimitService>();
        limitService.Setup(x => x.EnsureWithinLimitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateCustomerCommandHandler(
            context.Object,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            limitService.Object,
            TestHandlerDependencies.CreateLogger<CreateCustomerCommandHandler>());

        var act = () => handler.Handle(
            new CreateCustomerCommand(
                "Ali",
                "Khan",
                "ali@test.com",
                "123",
                "Street",
                "Lahore",
                "Pakistan",
                "54000"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_WithValidData_ReturnsId()
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(new List<Customer>().AsQueryable()).Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var limitService = new Mock<ITenantLimitService>();
        limitService.Setup(x => x.EnsureWithinLimitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateCustomerCommandHandler(
            context.Object,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            limitService.Object,
            TestHandlerDependencies.CreateLogger<CreateCustomerCommandHandler>());

        var id = await handler.Handle(
            new CreateCustomerCommand(
                "Ali",
                "Ahsan",
                "ali@test.com",
                "1234567890",
                "123 Main St",
                "Lahore",
                "Pakistan",
                "54000"),
            CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        context.Verify(x => x.Customers.Add(It.IsAny<Customer>()), Times.Once);
    }
}

public class UpdateCustomerHandlerTests
{
    [Fact]
    public async Task Handle_WithMissingCustomer_ThrowsNotFoundException()
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(new List<Customer>().AsQueryable()).Object);

        var handler = new UpdateCustomerCommandHandler(
            context.Object,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            TestHandlerDependencies.CreateLogger<UpdateCustomerCommandHandler>());

        var act = () => handler.Handle(
            new UpdateCustomerCommand(
                Guid.NewGuid(),
                "Ali",
                "Ahsan",
                "ali@test.com",
                "123",
                "Street",
                "Lahore",
                "Pakistan",
                "54000",
                true),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithValidData_UpdatesCustomer()
    {
        var customerId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = customerId,
            FirstName = "Ali",
            LastName = "Ahsan",
            Email = "ali@test.com",
            PhoneNumber = "123",
            Address = "Street",
            City = "Lahore",
            Country = "Pakistan",
            PostalCode = "54000",
            TenantId = Guid.NewGuid()
        };

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(new List<Customer> { customer }.AsQueryable()).Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateCustomerCommandHandler(
            context.Object,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            TestHandlerDependencies.CreateLogger<UpdateCustomerCommandHandler>());

        await handler.Handle(
            new UpdateCustomerCommand(
                customerId,
                "Ali",
                "Updated",
                "ali@test.com",
                "456",
                "New Street",
                "Karachi",
                "Pakistan",
                "75000",
                false),
            CancellationToken.None);

        customer.LastName.Should().Be("Updated");
        customer.IsActive.Should().BeFalse();
    }
}

public class DeleteCustomerHandlerTests
{
    [Fact]
    public async Task Handle_WithMissingCustomer_ThrowsNotFoundException()
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(new List<Customer>().AsQueryable()).Object);

        var handler = new DeleteCustomerCommandHandler(
            context.Object,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            TestHandlerDependencies.CreateLogger<DeleteCustomerCommandHandler>());

        var act = () => handler.Handle(new DeleteCustomerCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithExistingCustomer_SoftDeletes()
    {
        var customerId = Guid.NewGuid();
        var customer = new Customer
        {
            Id = customerId,
            FirstName = "Ali",
            LastName = "Ahsan",
            Email = "ali@test.com",
            PhoneNumber = "123",
            Address = "Street",
            City = "Lahore",
            Country = "Pakistan",
            PostalCode = "54000",
            IsActive = true,
            TenantId = Guid.NewGuid()
        };

        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(new List<Customer> { customer }.AsQueryable()).Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteCustomerCommandHandler(
            context.Object,
            TestHandlerDependencies.CreateBusinessEvents(),
            TestHandlerDependencies.CreateEntityAudit(),
            TestHandlerDependencies.CreateLogger<DeleteCustomerCommandHandler>());

        await handler.Handle(new DeleteCustomerCommand(customerId), CancellationToken.None);

        customer.IsActive.Should().BeFalse();
        context.Verify(x => x.Customers.Remove(customer), Times.Once);
    }
}

public class GetCustomerByIdHandlerTests
{
    [Fact]
    public async Task Handle_WithMissingCustomer_ThrowsNotFoundException()
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(new List<Customer>().AsQueryable()).Object);

        var handler = new GetCustomerByIdQueryHandler(context.Object);

        var act = () => handler.Handle(new GetCustomerByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class GetCustomerAnalyticsHandlerTests
{
    [Fact]
    public async Task Handle_WithOrders_ReturnsAnalytics()
    {
        var tenantId = Guid.NewGuid();
        var tenantProvider = new TenantProvider();
        tenantProvider.SetTenantId(tenantId);

        var options = new DbContextOptionsBuilder<BusinessOS.Infrastructure.Data.BusinessOSDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new BusinessOS.Infrastructure.Data.BusinessOSDbContext(options, tenantProvider);

        var customerId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            FirstName = "Ali",
            LastName = "Ahsan",
            Email = "ali@test.com",
            PhoneNumber = "123",
            Address = "Street",
            City = "Lahore",
            Country = "Pakistan",
            PostalCode = "54000"
        });

        db.Orders.AddRange(
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = customerId,
                OrderNumber = "ORD-1",
                Status = OrderStatusNames.Completed,
                GrandTotal = 100,
                TotalAmount = 100,
                OrderDate = DateTime.UtcNow.AddDays(-2)
            },
            new Order
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerId = customerId,
                OrderNumber = "ORD-2",
                Status = OrderStatusNames.Pending,
                GrandTotal = 50,
                TotalAmount = 50,
                OrderDate = DateTime.UtcNow
            });

        await db.SaveChangesAsync();

        var handler = new GetCustomerAnalyticsQueryHandler(db);

        var result = await handler.Handle(new GetCustomerAnalyticsQuery(customerId), CancellationToken.None);

        result.TotalOrders.Should().Be(2);
        result.TotalSpending.Should().Be(150);
        result.AverageOrderValue.Should().Be(75);
        result.TotalCompletedOrders.Should().Be(1);
        result.LastOrderDate.Should().NotBeNull();
    }
}

public class GetCustomerOrdersHandlerTests
{
    [Fact]
    public async Task Handle_WithMissingCustomer_ThrowsNotFoundException()
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Customers).Returns(TestMockDbSet.CreateMockDbSet(new List<Customer>().AsQueryable()).Object);

        var handler = new GetCustomerOrdersQueryHandler(
            context.Object,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<GetCustomerOrdersQueryHandler>>());

        var act = () => handler.Handle(
            new GetCustomerOrdersQuery(Guid.NewGuid(), 1, 10, null, BusinessOS.Application.Common.Models.SortDirection.Desc),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

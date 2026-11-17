using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Operations.DTOs;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.UnitTests.Operations;

public class Phase2BusinessOperationsTests
{
    [Fact]
    public async Task EmployeeSalary_IsHiddenWithoutPermission()
    {
        var (db, user) = Create();
        user.Permissions = [PermissionCodes.EmployeeManage, PermissionCodes.EmployeeView, PermissionCodes.EmployeeSalary];
        var service = new WorkforceService(db, user, NullLogger<WorkforceService>.Instance);

        await service.SaveEmployeeAsync(null, Employee(), CancellationToken.None);
        user.Permissions = [PermissionCodes.EmployeeView];
        var hidden = await service.GetEmployeesAsync(null, null, null, 1, 20, CancellationToken.None);

        hidden.Items.Single().Salary.Should().BeNull();
        (await db.Employees.SingleAsync()).Salary.Should().Be(1200);
    }

    [Fact]
    public async Task CheckIn_RejectsASecondOpenCheckIn()
    {
        var (db, user) = Create();
        user.Permissions = [PermissionCodes.EmployeeManage, PermissionCodes.EmployeeView, PermissionCodes.AttendanceManage, PermissionCodes.EmployeeSalary];
        var workforce = new WorkforceService(db, user, NullLogger<WorkforceService>.Instance);
        var employee = await workforce.SaveEmployeeAsync(null, Employee() with { UserId = user.UserId, Salary = null }, CancellationToken.None);

        await workforce.CheckInAsync(employee.Id, CancellationToken.None);
        var act = () => workforce.CheckInAsync(employee.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task LeaveApproval_ConsumesBalance_AndRejectsOverdraw()
    {
        var (db, user) = Create();
        user.Permissions = [PermissionCodes.EmployeeManage, PermissionCodes.EmployeeView, PermissionCodes.LeaveManage, PermissionCodes.EmployeeSalary];
        var workforce = new WorkforceService(db, user, NullLogger<WorkforceService>.Instance);
        var employee = await workforce.SaveEmployeeAsync(null, Employee(), CancellationToken.None);
        var type = await workforce.SaveLeaveTypeAsync(new LeaveTypeRequest("Annual", 1, true), CancellationToken.None);

        var first = await workforce.ApplyLeaveAsync(new LeaveRequestCreate(type.Id, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 1), "Trip", employee.Id), CancellationToken.None);
        var approved = await workforce.DecideLeaveAsync(first.Id, true, null, CancellationToken.None);
        approved.Status.Should().Be("Approved");

        var second = await workforce.ApplyLeaveAsync(new LeaveRequestCreate(type.Id, new DateOnly(2026, 12, 3), new DateOnly(2026, 12, 3), "More", employee.Id), CancellationToken.None);
        var act = () => workforce.DecideLeaveAsync(second.Id, true, null, CancellationToken.None);
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task LeaveList_IsLimitedToTheCurrentEmployee()
    {
        var (db, user) = Create();
        user.Permissions = [PermissionCodes.EmployeeManage, PermissionCodes.EmployeeView, PermissionCodes.LeaveManage, PermissionCodes.EmployeeSalary];
        var workforce = new WorkforceService(db, user, NullLogger<WorkforceService>.Instance);
        var owner = await workforce.SaveEmployeeAsync(null, Employee() with { UserId = user.UserId }, CancellationToken.None);
        var other = await workforce.SaveEmployeeAsync(null, Employee() with { Email = "other@company.test", FirstName = "Other" }, CancellationToken.None);
        var type = await workforce.SaveLeaveTypeAsync(new LeaveTypeRequest("Annual", 5, true), CancellationToken.None);
        await workforce.ApplyLeaveAsync(new LeaveRequestCreate(type.Id, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 1), null, other.Id), CancellationToken.None);

        user.Permissions = [PermissionCodes.EmployeeView];
        var page = await workforce.GetLeaveAsync(null, 1, 20, CancellationToken.None);

        page.Items.Should().BeEmpty();
        owner.Id.Should().NotBe(other.Id);
    }

    [Fact]
    public async Task LeadConversion_LinksExistingCustomer_InsteadOfDuplicating()
    {
        var (db, user) = Create();
        user.Permissions = [PermissionCodes.LeadManage, PermissionCodes.CustomerCreate, PermissionCodes.DealManage];
        db.Customers.Add(new Customer
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@company.test",
            PhoneNumber = "1",
            Address = "1 Road",
            City = "London",
            Country = "UK",
            PostalCode = "EC1"
        });
        await db.SaveChangesAsync();
        var crm = new CrmOperationsService(db, user);
        var lead = await crm.SaveLeadAsync(null, new LeadRequest("Ada Lovelace", "Analytical", "ada@company.test", "1", "Referral", "High", null, user.UserId, 5000, null, null), CancellationToken.None);

        var result = await crm.ConvertLeadAsync(lead.Id, true, CancellationToken.None);

        result.CustomerCreated.Should().BeFalse();
        (await db.Customers.CountAsync()).Should().Be(1);
        (await db.Deals.CountAsync(x => x.Status == "Won")).Should().Be(1);
    }

    [Fact]
    public async Task Transfer_RejectsNegativeStock_AndDoesNotChangeCompanyStock()
    {
        var (db, user) = Create();
        user.Permissions = [PermissionCodes.WarehouseManage];
        var from = await db.Warehouses.AddAsync(new Warehouse { Name = "A", Code = "A" });
        var to = await db.Warehouses.AddAsync(new Warehouse { Name = "B", Code = "B" });
        var product = await db.Products.AddAsync(new Product { Name = "Box", SKU = "BOX", CategoryId = Guid.NewGuid(), CurrentStock = 4 });
        await db.SaveChangesAsync();
        db.WarehouseStocks.Add(new WarehouseStock { WarehouseId = from.Entity.Id, ProductId = product.Entity.Id, OnHand = 2 });
        await db.SaveChangesAsync();
        var commerce = Commerce(db, user);

        var act = () => commerce.TransferAsync(new TransferRequest(from.Entity.Id, to.Entity.Id, null, [new TransferLineRequest(product.Entity.Id, 5)]), CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
        (await db.Products.SingleAsync()).CurrentStock.Should().Be(4);
    }

    [Fact]
    public async Task ProfitAndLoss_UsesInvoicesCostsAndExpenses()
    {
        var (db, user) = Create();
        user.Permissions = [PermissionCodes.FinanceView];
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        db.Orders.Add(new Order
        {
            Id = orderId,
            CustomerId = customerId,
            OrderNumber = "SO-1",
            OrderDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
            Status = "Completed",
            GrandTotal = 100,
            OrderItems = [new OrderItem { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 50, UnitCost = 20, Total = 100 }]
        });
        db.Invoices.Add(new Invoice
        {
            InvoiceNumber = "INV-1",
            OrderId = orderId,
            CustomerId = customerId,
            InvoiceDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc),
            Status = "Issued",
            GrandTotal = 100,
            OutstandingAmount = 100
        });
        db.Payments.Add(new Payment
        {
            OrderId = orderId,
            CustomerId = customerId,
            Amount = 40,
            RefundedAmount = 10,
            PaymentMethod = "Cash",
            PaymentDate = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc),
            Status = "Completed"
        });
        db.Expenses.Add(new Expense
        {
            Title = "Rent",
            Amount = 15,
            ExpenseDate = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
            Status = "Approved",
            PaymentMethod = "Bank transfer"
        });
        await db.SaveChangesAsync();

        var report = await Commerce(db, user).GetProfitAndLossAsync(
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        report.RecognizedRevenue.Should().Be(100);
        report.CashReceived.Should().Be(30);
        report.CostOfGoodsSold.Should().Be(40);
        report.OperatingExpenses.Should().Be(15);
        report.NetProfit.Should().Be(45);
    }

    [Fact]
    public async Task Employees_AreIsolatedByTenant()
    {
        var database = Guid.NewGuid().ToString();
        var (left, leftUser) = Create(database, Guid.NewGuid());
        var (right, rightUser) = Create(database, Guid.NewGuid());
        leftUser.Permissions = [PermissionCodes.EmployeeManage, PermissionCodes.EmployeeView, PermissionCodes.EmployeeSalary];
        rightUser.Permissions = [PermissionCodes.EmployeeView];
        var leftService = new WorkforceService(left, leftUser, NullLogger<WorkforceService>.Instance);
        await leftService.SaveEmployeeAsync(null, Employee(), CancellationToken.None);

        var visible = await new WorkforceService(right, rightUser, NullLogger<WorkforceService>.Instance)
            .GetEmployeesAsync(null, null, null, 1, 20, CancellationToken.None);

        visible.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Portal_CannotReadAnotherCustomersProject()
    {
        var (db, user) = Create();
        user.Permissions = [PermissionCodes.ProjectCreate, PermissionCodes.ProjectUpdate, PermissionCodes.PortalManage, PermissionCodes.ProjectView];
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        db.Customers.AddRange(
            new Customer { Id = customerA, FirstName = "A", LastName = "A", Email = "a@company.test", PhoneNumber = "1", Address = "a", City = "a", Country = "a", PostalCode = "a" },
            new Customer { Id = customerB, FirstName = "B", LastName = "B", Email = "b@company.test", PhoneNumber = "1", Address = "b", City = "b", Country = "b", PostalCode = "b" });
        await db.SaveChangesAsync();
        var projects = new ProjectOperationsService(db, user, new TenantProvider(), new ConfigurationBuilder().Build());
        var hidden = await projects.SaveProjectAsync(null, new ProjectRequest("Secret", null, null, customerB, user.UserId, null, null, 0, 1, "Software", "Internal", null), CancellationToken.None);
        var visible = await projects.SaveProjectAsync(null, new ProjectRequest("Shared", null, null, customerA, user.UserId, null, null, 0, 1, "Software", "Internal", null), CancellationToken.None);
        var access = await projects.CreateClientAccessAsync(visible.Id, "client@company.test", CancellationToken.None);

        var portal = await projects.GetPortalAsync(access.Token, CancellationToken.None);

        portal.Select(x => x.Id).Should().Equal(visible.Id);
        portal.Should().NotContain(x => x.Id == hidden.Id);
    }

    [Fact]
    public async Task Timer_RejectsASecondActiveTimer()
    {
        var (db, user) = Create();
        user.Permissions = [PermissionCodes.TimeView];
        var projects = new ProjectOperationsService(db, user, new TenantProvider(), new ConfigurationBuilder().Build());
        await projects.StartTimerAsync(null, null, true, null, CancellationToken.None);

        var act = () => projects.StartTimerAsync(null, null, false, null, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    private static CommerceOperationsService Commerce(BusinessOSDbContext db, TestUser user) =>
        new(db, user, null!, null!);

    private static (BusinessOSDbContext Db, TestUser User) Create(string? database = null, Guid? tenantId = null)
    {
        var tenant = tenantId ?? Guid.NewGuid();
        var provider = new TenantProvider();
        provider.SetTenantId(tenant);
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(database ?? Guid.NewGuid().ToString())
            .Options;
        var user = new TestUser { TenantId = tenant, UserId = "user-1" };
        return (new BusinessOSDbContext(options, provider), user);
    }

    private static EmployeeRequest Employee() =>
        new("Ada", "Lovelace", "ada@company.test", "123", "Engineer", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            null, null, null, "FullTime", "C#", "Remote", "Ann", "999", 1200, null, null);

    private sealed class TestUser : ICurrentUserService
    {
        public string? UserId { get; set; }
        public string? Email { get; set; } = "ada@company.test";
        public Guid? TenantId { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = [];
        public List<string> Permissions { get; set; } = [];
        IReadOnlyList<string> ICurrentUserService.Permissions => Permissions;
        public bool HasPermission(string permissionCode) =>
            Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }
}

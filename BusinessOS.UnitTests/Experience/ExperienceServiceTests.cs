using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Experience;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.UnitTests.Experience;

public class ExperienceServiceTests
{
    [Fact]
    public async Task Unpublished_storefront_is_not_public()
    {
        var (service, _) = Create();
        await service.SaveStorefrontAsync(new StorefrontWriteRequest { IsPublished = false, PublicEmail = "hello@acme.test" }, CancellationToken.None);

        var result = await service.GetPublicCompanyAsync("acme", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Inquiry_creates_a_lead_only_for_the_target_company()
    {
        var (service, harness) = Create();
        await service.SaveStorefrontAsync(new StorefrontWriteRequest { IsPublished = true, PublicEmail = "hello@acme.test" }, CancellationToken.None);

        var submitted = await service.SubmitInquiryAsync("acme", new PublicInquiryRequest
        {
            Name = "Noor Ali",
            Email = "noor@client.test",
            Message = "We need a logistics proposal.",
            Consent = true,
            ClientToken = "inquiry-1"
        }, CancellationToken.None);

        submitted.LeadId.Should().NotBeNull();
        var lead = await harness.Db.Leads.SingleAsync();
        lead.Source.Should().Be("Public storefront");
        lead.Email.Should().Be("noor@client.test");
        lead.TenantId.Should().Be(harness.TenantId);

        harness.Provider.SetTenantId(Guid.NewGuid());
        await using var other = harness.Factory.CreateDbContext();
        (await other.Leads.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Insights_use_recorded_stock_and_stay_empty_without_it()
    {
        var (service, harness) = Create();
        harness.User.Permissions.Add(PermissionCodes.ProductView);
        harness.Db.Products.Add(new Product
        {
            Name = "Packing boxes",
            SKU = "BOX-1",
            CategoryId = Guid.NewGuid(),
            CurrentStock = 1,
            ReorderLevel = 5,
            IsActive = true,
            RowVersion = new byte[8]
        });
        await harness.Db.SaveChangesAsync();

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        dashboard.Insights.Should().Contain(x => x.Message.Contains("Packing boxes") && x.Message.Contains("5"));
        dashboard.Insights.Should().NotContain(x => x.Code == "revenue-window");

        var product = await harness.Db.Products.SingleAsync();
        product.CurrentStock = 20;
        await harness.Db.SaveChangesAsync();
        var cleared = await service.GetDashboardAsync(CancellationToken.None);
        cleared.Insights.Should().NotContain(x => x.Code == "low-stock");
    }

    [Fact]
    public async Task Public_order_uses_the_stored_product_price()
    {
        var (service, harness) = Create();
        await service.SaveStorefrontAsync(new StorefrontWriteRequest { IsPublished = true }, CancellationToken.None);
        var product = new Product
        {
            Name = "Notebook",
            SKU = "NB-1",
            CategoryId = Guid.NewGuid(),
            SalePrice = 25,
            CostPrice = 10,
            CurrentStock = 8,
            IsActive = true,
            Kind = "Physical",
            RowVersion = new byte[8]
        };
        harness.Db.Products.Add(product);
        await harness.Db.SaveChangesAsync();
        await service.SaveOfferingAsync(new StorefrontOfferingWrite
        {
            Kind = "product",
            ProductId = product.Id,
            Name = "Notebook",
            PricingModel = "fixed",
            Price = 1,
            IsPublished = true
        }, CancellationToken.None);

        var submitted = await service.SubmitOrderAsync("acme", new PublicOrderRequest
        {
            OfferingId = (await harness.Db.StorefrontOfferings.SingleAsync()).Id,
            Quantity = 2,
            Name = "Noor Ali",
            Email = "noor@client.test",
            Phone = "555",
            Address = "12 Market Road",
            City = "Lahore",
            Country = "Pakistan",
            PostalCode = "54000",
            Consent = true,
            ClientToken = "order-1"
        }, CancellationToken.None);

        var order = await harness.Db.Orders.Include(x => x.OrderItems).SingleAsync();
        order.GrandTotal.Should().Be(50);
        order.OrderItems.Single().UnitPrice.Should().Be(25);
        order.Status.Should().Be("Pending");
        order.PaymentStatus.Should().Be("Unpaid");
        submitted.OrderNumber.Should().Be(order.OrderNumber);
        (await harness.Db.Leads.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Invalid_slug_is_rejected()
    {
        var (service, _) = Create();
        var act = () => service.SaveStorefrontAsync(new StorefrontWriteRequest { Slug = "Bad Slug" }, CancellationToken.None);
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Employee_layout_hides_finance_widgets()
    {
        var (service, harness) = Create();
        harness.User.Roles = ["Employee"];
        harness.User.Permissions.Clear();
        harness.User.Permissions.Add(PermissionCodes.TaskView);

        var dashboard = await service.GetDashboardAsync(CancellationToken.None);

        dashboard.Widgets.Where(x => x.Visible).Select(x => x.Id).Should().BeEquivalentTo(["tasks", "projects", "insights"]);
        dashboard.Revenue.Should().BeNull();
        dashboard.ActiveAgents.Should().BeNull();
    }

    private static (ExperienceService Service, Harness Harness) Create()
    {
        var tenantId = Guid.NewGuid();
        var provider = new TenantProvider();
        provider.SetTenantId(tenantId);
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new BusinessOSDbContext(options, provider);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            Slug = "acme",
            BusinessType = "software-agency",
            Email = "owner@acme.test",
            Phone = "555",
            Address = "Office",
            Country = "Pakistan",
            Currency = "USD",
            OwnerUserId = "owner-1",
            SubscriptionPlanId = Guid.NewGuid(),
            Status = TenantStatus.Active,
            IsActive = true
        });
        db.SaveChanges();
        var user = new TestUser
        {
            TenantId = tenantId,
            UserId = "owner-1",
            Roles = ["Owner"],
            Permissions =
            [
                PermissionCodes.OrganizationManage,
                PermissionCodes.OrganizationView,
                PermissionCodes.StorefrontManage,
                PermissionCodes.ProductView
            ]
        };
        var factory = new MemoryFactory(options, provider);
        var service = new ExperienceService(db, factory, user, provider);
        return (service, new Harness(db, provider, factory, user, tenantId));
    }

    private sealed record Harness(BusinessOSDbContext Db, TenantProvider Provider, MemoryFactory Factory, TestUser User, Guid TenantId);

    private sealed class MemoryFactory : IDbContextFactory<BusinessOSDbContext>
    {
        private readonly DbContextOptions<BusinessOSDbContext> _options;
        private readonly TenantProvider _provider;
        public MemoryFactory(DbContextOptions<BusinessOSDbContext> options, TenantProvider provider)
        {
            _options = options;
            _provider = provider;
        }
        public BusinessOSDbContext CreateDbContext() => new(_options, _provider);
        public Task<BusinessOSDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class TestUser : ICurrentUserService
    {
        public string? UserId { get; set; }
        public string? Email { get; set; } = "owner@acme.test";
        public Guid? TenantId { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = [];
        public List<string> Permissions { get; set; } = [];
        IReadOnlyList<string> ICurrentUserService.Permissions => Permissions;
        public bool HasPermission(string permissionCode) => Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }
}

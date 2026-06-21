using System.Reflection.Emit;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Persistence.Context;

public class BusinessOSDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantProvider _tenantProvider;
    public BusinessOSDbContext(DbContextOptions<BusinessOSDbContext> options,
        ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<AIConversation> AIConversations => Set<AIConversation>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global filter for multi-tenancy (IMPORTANT)
        builder.Entity<Product>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Category>().HasQueryFilter(x => !x.IsDeleted);

        // You will extend this later with Tenant filter middleware
        builder.Entity<Product>()
        .HasQueryFilter(x => x.TenantId == _tenantProvider.GetTenantId());

        builder.Entity<Category>()
            .HasQueryFilter(x => x.TenantId == _tenantProvider.GetTenantId());

        builder.Entity<Customer>()
            .HasQueryFilter(x => x.TenantId == _tenantProvider.GetTenantId());

        builder.Entity<Order>()
            .HasQueryFilter(x => x.TenantId == _tenantProvider.GetTenantId());

        builder.Entity<Expense>()
            .HasQueryFilter(x => x.TenantId == _tenantProvider.GetTenantId());

        builder.Entity<Supplier>()
            .HasQueryFilter(x => x.TenantId == _tenantProvider.GetTenantId());
    }
}

using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data.Configurations;
using BusinessOS.Infrastructure.Identity;
using BusinessOS.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Data;

public class BusinessOSDbContext : IdentityDbContext<ApplicationUser>
{
    public BusinessOSDbContext(DbContextOptions<BusinessOSDbContext> options)
        : base(options)
    {
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
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<AIConversation> AIConversations => Set<AIConversation>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(CustomerConfiguration).Assembly);

        ConfigureGlobalQueryFilters(builder);
    }

    private static void ConfigureGlobalQueryFilters(ModelBuilder builder)
    {
        builder.Entity<Product>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId && !x.IsDeleted);

        builder.Entity<Category>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId && !x.IsDeleted);

        builder.Entity<Customer>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);

        builder.Entity<Supplier>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);

        builder.Entity<Order>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);

        builder.Entity<OrderItem>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);

        builder.Entity<Expense>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);

        builder.Entity<Employee>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);

        builder.Entity<InventoryTransaction>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);

        builder.Entity<Purchase>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);

        builder.Entity<PurchaseItem>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);

        builder.Entity<Payment>()
            .HasQueryFilter(x => x.TenantId == TenantContext.CurrentTenantId);
    }
}

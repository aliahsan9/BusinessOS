using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data.Configurations;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Data;

public class BusinessOSDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>, IApplicationDbContext
{
    private readonly Guid _tenantId;

    public BusinessOSDbContext(
        DbContextOptions<BusinessOSDbContext> options,
        ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantId = tenantProvider.HasTenant()
            ? tenantProvider.TenantId
            : Guid.Empty;
    }

    // DbSets
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
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<Role> RbacRoles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> RbacUserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RbacAuditLog> RbacAuditLogs => Set<RbacAuditLog>();
    public DbSet<AIConversation> AIConversations => Set<AIConversation>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        ApplyTenantAndAuditRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTenantAndAuditRules()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            // Assign TenantId automatically
            if (entry.State == EntityState.Added)
            {
                var tenantIdProp = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "TenantId");

                if (tenantIdProp is not null)
                {
                    var currentValue = tenantIdProp.CurrentValue as Guid? ?? Guid.Empty;
                    if (currentValue == Guid.Empty && _tenantId != Guid.Empty)
                    {
                        tenantIdProp.CurrentValue = _tenantId;
                    }
                }
            }

            // Soft delete handling (if entity has IsDeleted)
            if (entry.State == EntityState.Deleted)
            {
                var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");

                if (isDeletedProp != null)
                {
                    entry.State = EntityState.Modified;
                    isDeletedProp.CurrentValue = true;
                }
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(CustomerConfiguration).Assembly);

        ConfigureGlobalQueryFilters(builder);
    }

    private void ConfigureGlobalQueryFilters(ModelBuilder builder)
    {
        // Tenant + Soft Delete safe filter pattern

        builder.Entity<Product>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Category>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Customer>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Supplier>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Order>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<OrderItem>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Expense>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<ExpenseCategory>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Notification>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<TenantSettings>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Employee>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Inventory>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<StockTransaction>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Purchase>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<PurchaseItem>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Payment>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Invoice>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Quotation>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<QuotationItem>()
            .HasQueryFilter(x => x.TenantId == _tenantId && !x.IsDeleted);

        builder.Entity<Tenant>()
            .HasQueryFilter(x => x.Id == _tenantId && !x.IsDeleted);
    }
}

using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Purchase> Purchases { get; }
    DbSet<PurchaseItem> PurchaseItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Quotation> Quotations { get; }
    DbSet<QuotationItem> QuotationItems { get; }
    DbSet<Inventory> Inventories { get; }
    DbSet<StockTransaction> StockTransactions { get; }
    DbSet<Role> RbacRoles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> RbacUserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RbacAuditLog> RbacAuditLogs { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Activity> Activities { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<TenantSubscription> TenantSubscriptions { get; }
    DbSet<TenantUsage> TenantUsages { get; }
    DbSet<TenantAuditLog> TenantAuditLogs { get; }
    DbSet<BillingInvoice> BillingInvoices { get; }
    DbSet<BillingTransaction> BillingTransactions { get; }
    DbSet<PaymentProvider> PaymentProviders { get; }
    DbSet<TenantSettings> TenantSettings { get; }
    DbSet<GeneratedReport> GeneratedReports { get; }
    DbSet<UserOnboardingProgress> UserOnboardingProgress { get; }
    DbSet<AIConversation> AIConversations { get; }
    DbSet<TeamInvitation> TeamInvitations { get; }
    DbSet<Project> Projects { get; }
    DbSet<WorkTask> WorkTasks { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}

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
    DbSet<Inventory> Inventories { get; }
    DbSet<StockTransaction> StockTransactions { get; }
    DbSet<Role> RbacRoles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> RbacUserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RbacAuditLog> RbacAuditLogs { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}

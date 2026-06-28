using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Inventory> Inventories { get; }
    DbSet<StockTransaction> StockTransactions { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}

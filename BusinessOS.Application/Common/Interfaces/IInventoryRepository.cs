using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Common.Interfaces;

public interface IInventoryRepository
{
    Task<Inventory?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<Inventory?> GetByProductIdForUpdateAsync(Guid productId, CancellationToken cancellationToken = default);
    Task AddAsync(Inventory inventory, CancellationToken cancellationToken = default);
    Task UpdateAsync(Inventory inventory, CancellationToken cancellationToken = default);
    Task<List<Inventory>> GetLowStockAsync(CancellationToken cancellationToken = default);
    Task<List<Inventory>> GetOutOfStockAsync(CancellationToken cancellationToken = default);
    Task<List<Inventory>> GetReorderProductsAsync(CancellationToken cancellationToken = default);
    IQueryable<Inventory> Query();
}

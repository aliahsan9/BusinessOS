using BusinessOS.Domain.Entities;
using BusinessOS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Repositories;

public sealed class InventoryRepository : IInventoryRepository
{
    private readonly IApplicationDbContext _context;

    public InventoryRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Inventory?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _context.Inventories
            .AsNoTracking()
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken);

    public Task<Inventory?> GetByProductIdForUpdateAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _context.Inventories
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.ProductId == productId, cancellationToken);

    public async Task AddAsync(Inventory inventory, CancellationToken cancellationToken = default)
    {
        _context.Inventories.Add(inventory);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Inventory inventory, CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Inventory>> GetLowStockAsync(CancellationToken cancellationToken = default) =>
        _context.Inventories
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel)
            .OrderBy(x => x.CurrentStock)
            .ToListAsync(cancellationToken);

    public Task<List<Inventory>> GetOutOfStockAsync(CancellationToken cancellationToken = default) =>
        _context.Inventories
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.CurrentStock <= 0)
            .OrderBy(x => x.Product!.Name)
            .ToListAsync(cancellationToken);

    public Task<List<Inventory>> GetReorderProductsAsync(CancellationToken cancellationToken = default) =>
        _context.Inventories
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.CurrentStock <= x.ReorderLevel)
            .OrderBy(x => x.CurrentStock)
            .ToListAsync(cancellationToken);

    public IQueryable<Inventory> Query() =>
        _context.Inventories.AsNoTracking().Include(x => x.Product);
}

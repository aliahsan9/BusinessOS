using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Repositories;

public sealed class StockTransactionRepository : IStockTransactionRepository
{
    private readonly IApplicationDbContext _context;

    public StockTransactionRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StockTransaction transaction, CancellationToken cancellationToken = default)
    {
        _context.StockTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public IQueryable<StockTransaction> Query() =>
        _context.StockTransactions.AsNoTracking().Include(x => x.Product);
}

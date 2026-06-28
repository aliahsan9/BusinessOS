using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Common.Interfaces;

public interface IStockTransactionRepository
{
    Task AddAsync(StockTransaction transaction, CancellationToken cancellationToken = default);
    IQueryable<StockTransaction> Query();
}

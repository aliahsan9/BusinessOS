using BusinessOS.Application.Common.Concurrency;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BusinessOS.Infrastructure.Data;

public partial class BusinessOSDbContext
{
    public bool HasActiveTransaction => Database.CurrentTransaction is not null;

    public async Task<IApplicationTransaction?> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null)
            return null;

        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfApplicationTransaction(transaction);
    }

    public void Detach<TEntity>(TEntity entity) where TEntity : class
    {
        var entry = Entry(entity);
        if (entry.State != EntityState.Detached)
            entry.State = EntityState.Detached;
    }

    public async Task<StockAdjustmentResult> TryAdjustStockAsync(
        Guid productId,
        decimal quantityDelta,
        CancellationToken cancellationToken = default)
    {
        if (quantityDelta == 0 || _tenantId == Guid.Empty)
            return StockAdjustmentResult.Rejected(0);

        var now = DateTime.UtcNow;
        DetachInventoryGraph(productId);

        if (Database.IsRelational())
        {
            var rowsQuery = Inventories.Where(i =>
                i.ProductId == productId &&
                i.TenantId == _tenantId &&
                !i.IsDeleted);

            if (quantityDelta < 0)
            {
                var required = -quantityDelta;
                rowsQuery = rowsQuery.Where(i => i.CurrentStock >= required);
            }

            var rows = await rowsQuery.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.CurrentStock, i => i.CurrentStock + quantityDelta)
                    .SetProperty(i => i.LastUpdated, now)
                    .SetProperty(i => i.UpdatedAt, now),
                cancellationToken);

            if (rows != 1)
            {
                var available = await Inventories.AsNoTracking()
                    .Where(i => i.ProductId == productId && i.TenantId == _tenantId && !i.IsDeleted)
                    .Select(i => (decimal?)i.CurrentStock)
                    .FirstOrDefaultAsync(cancellationToken);

                return available.HasValue
                    ? StockAdjustmentResult.Rejected(available.Value)
                    : StockAdjustmentResult.Missing;
            }

            var newStock = await Inventories.AsNoTracking()
                .Where(i => i.ProductId == productId && i.TenantId == _tenantId && !i.IsDeleted)
                .Select(i => i.CurrentStock)
                .FirstAsync(cancellationToken);

            var productRows = await Products
                .Where(p => p.Id == productId && p.TenantId == _tenantId && !p.IsDeleted)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(p => p.CurrentStock, newStock)
                        .SetProperty(p => p.UpdatedAt, now),
                    cancellationToken);

            if (productRows != 1)
                throw new InvalidOperationException("Inventory changed without a matching product row.");

            return StockAdjustmentResult.Ok(newStock - quantityDelta, newStock);
        }

        var inventory = await Inventories
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.TenantId == _tenantId, cancellationToken);

        if (inventory is null)
            return StockAdjustmentResult.Missing;

        var previous = inventory.CurrentStock;
        var updated = previous + quantityDelta;
        if (updated < 0)
            return StockAdjustmentResult.Rejected(previous);

        inventory.CurrentStock = updated;
        inventory.LastUpdated = now;
        inventory.UpdatedAt = now;
        if (inventory.Product is not null)
        {
            inventory.Product.CurrentStock = updated;
            inventory.Product.UpdatedAt = now;
        }

        return StockAdjustmentResult.Ok(previous, updated);
    }

    public Task<bool> TryTransitionOrderStatusAsync(
        Guid orderId,
        string expectedStatus,
        string newStatus,
        CancellationToken cancellationToken = default) =>
        TryTransitionStatusAsync(Orders, orderId, expectedStatus, newStatus, cancellationToken);

    public Task<bool> TryTransitionPurchaseStatusAsync(
        Guid purchaseId,
        string expectedStatus,
        string newStatus,
        CancellationToken cancellationToken = default) =>
        TryTransitionStatusAsync(Purchases, purchaseId, expectedStatus, newStatus, cancellationToken);

    public async Task<int> TryRefundPaymentAsync(
        Guid paymentId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0 || _tenantId == Guid.Empty)
            return 0;

        var now = DateTime.UtcNow;
        if (Database.IsRelational())
        {
            DetachPayments(paymentId);
            return await Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE Payments
                SET RefundedAmount = RefundedAmount + {amount},
                    Status = CASE
                        WHEN RefundedAmount + {amount} >= Amount THEN {PaymentStatusNames.Refunded}
                        ELSE Status
                    END,
                    UpdatedAt = {now}
                WHERE Id = {paymentId}
                  AND TenantId = {_tenantId}
                  AND IsDeleted = 0
                  AND Status = {PaymentStatusNames.Completed}
                  AND RefundedAmount + {amount} <= Amount
                """, cancellationToken);
        }

        var payment = await Payments.FirstOrDefaultAsync(
            p => p.Id == paymentId && p.TenantId == _tenantId,
            cancellationToken);

        if (payment is null ||
            !string.Equals(payment.Status, PaymentStatusNames.Completed, StringComparison.OrdinalIgnoreCase) ||
            payment.RefundedAmount + amount > payment.Amount)
        {
            return 0;
        }

        payment.RefundedAmount += amount;
        payment.UpdatedAt = now;
        if (payment.RefundedAmount >= payment.Amount)
            payment.Status = PaymentStatusNames.Refunded;

        return 1;
    }

    private async Task<bool> TryTransitionStatusAsync<TEntity>(
        DbSet<TEntity> set,
        Guid id,
        string expectedStatus,
        string newStatus,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var now = DateTime.UtcNow;
        if (Database.IsRelational())
        {
            var rows = await set
                .Where(entity => EF.Property<Guid>(entity, "Id") == id
                    && EF.Property<Guid>(entity, "TenantId") == _tenantId
                    && !EF.Property<bool>(entity, "IsDeleted")
                    && EF.Property<string>(entity, "Status") == expectedStatus)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(entity => EF.Property<string>(entity, "Status"), newStatus)
                        .SetProperty(entity => EF.Property<DateTime?>(entity, "UpdatedAt"), now),
                    cancellationToken);

            if (rows == 1)
                await ReloadIfTrackedAsync<TEntity>(id, cancellationToken);

            return rows == 1;
        }

        var tracked = await set.FirstOrDefaultAsync(
            entity => EF.Property<Guid>(entity, "Id") == id,
            cancellationToken);

        if (tracked is null)
            return false;

        var entry = Entry(tracked);
        var status = entry.Property("Status").CurrentValue as string;
        if (!string.Equals(status, expectedStatus, StringComparison.Ordinal))
            return false;

        entry.Property("Status").CurrentValue = newStatus;
        if (entry.Properties.Any(property => property.Metadata.Name == "UpdatedAt"))
            entry.Property("UpdatedAt").CurrentValue = now;

        return true;
    }

    private void DetachInventoryGraph(Guid productId)
    {
        foreach (var entry in ChangeTracker.Entries<Inventory>()
                     .Where(entry => entry.Entity.ProductId == productId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in ChangeTracker.Entries<Product>()
                     .Where(entry => entry.Entity.Id == productId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private void DetachPayments(Guid paymentId)
    {
        foreach (var entry in ChangeTracker.Entries<Payment>()
                     .Where(entry => entry.Entity.Id == paymentId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task ReloadIfTrackedAsync<TEntity>(Guid id, CancellationToken cancellationToken)
        where TEntity : class
    {
        var entry = ChangeTracker.Entries<TEntity>()
            .FirstOrDefault(candidate => candidate.Property("Id").CurrentValue is Guid value && value == id);
        if (entry is not null)
            await entry.ReloadAsync(cancellationToken);
    }

    private sealed class EfApplicationTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfApplicationTransaction(IDbContextTransaction transaction) =>
            _transaction = transaction;

        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            _transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            _transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}

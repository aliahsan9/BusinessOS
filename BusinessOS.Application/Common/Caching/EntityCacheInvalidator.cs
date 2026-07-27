namespace BusinessOS.Application.Common.Caching;

/// <summary>
/// Centralized cache invalidation helpers for write operations.
/// Removes related list, detail, dashboard, and inventory entries so clients never see stale data.
/// </summary>
public static class EntityCacheInvalidator
{
    public static Task InvalidateProductAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.ProductsPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.InventoryPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ReportsPrefix(tenantId), cancellationToken)
        };

        if (productId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.ProductById(tenantId, productId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidateCategoryAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.CategoriesPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ProductsPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken)
        };

        if (categoryId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.CategoryById(tenantId, categoryId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidateCustomerAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.CustomersPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ReportsPrefix(tenantId), cancellationToken)
        };

        if (customerId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.CustomerById(tenantId, customerId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidateSupplierAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.SuppliersPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.PurchaseOrdersPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken)
        };

        if (supplierId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.SupplierById(tenantId, supplierId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidateInventoryAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.InventoryPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ProductsPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ReportsPrefix(tenantId), cancellationToken)
        };

        if (productId.HasValue)
        {
            tasks.Add(cache.RemoveAsync(CacheKeys.InventoryByProduct(tenantId, productId.Value), cancellationToken));
            tasks.Add(cache.RemoveAsync(CacheKeys.ProductById(tenantId, productId.Value), cancellationToken));
        }

        return Task.WhenAll(tasks);
    }

    public static Task InvalidateOrderAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? orderId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.OrdersPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.InventoryPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ProductsPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.CustomersPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ReportsPrefix(tenantId), cancellationToken)
        };

        if (orderId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.OrderById(tenantId, orderId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidateInvoiceAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? invoiceId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.InvoicesPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ReportsPrefix(tenantId), cancellationToken)
        };

        if (invoiceId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.InvoiceById(tenantId, invoiceId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidatePaymentAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? paymentId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.PaymentsPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.InvoicesPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ReportsPrefix(tenantId), cancellationToken)
        };

        if (paymentId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.PaymentById(tenantId, paymentId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidatePurchaseOrderAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? purchaseOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.PurchaseOrdersPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.SuppliersPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.InventoryPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ProductsPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ReportsPrefix(tenantId), cancellationToken)
        };

        if (purchaseOrderId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.PurchaseOrderById(tenantId, purchaseOrderId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidateQuotationAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? quotationId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.QuotationsPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.OrdersPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken)
        };

        if (quotationId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.QuotationById(tenantId, quotationId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidateExpenseAsync(
        ICacheService cache,
        Guid tenantId,
        Guid? expenseId = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveByPrefixAsync(CacheKeys.ExpensesPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken),
            cache.RemoveByPrefixAsync(CacheKeys.ReportsPrefix(tenantId), cancellationToken)
        };

        if (expenseId.HasValue)
            tasks.Add(cache.RemoveAsync(CacheKeys.ExpenseById(tenantId, expenseId.Value), cancellationToken));

        return Task.WhenAll(tasks);
    }

    public static Task InvalidateDashboardAsync(
        ICacheService cache,
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        cache.RemoveByPrefixAsync(CacheKeys.DashboardPrefix(tenantId), cancellationToken);
}

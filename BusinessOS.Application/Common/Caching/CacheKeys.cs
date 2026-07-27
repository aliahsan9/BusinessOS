namespace BusinessOS.Application.Common.Caching;

/// <summary>
/// Tenant-aware cache key factory. Every key includes <c>Tenant_{tenantId}_</c>
/// so tenants never share cache entries.
/// </summary>
public static class CacheKeys
{
    public static string TenantRoot(Guid tenantId) => $"Tenant_{tenantId}_";

    // ── Products ──────────────────────────────────────────────────────────
    public static string ProductsPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Product";

    public static string ProductsAll(
        Guid tenantId,
        int page,
        int pageSize,
        Guid? categoryId,
        string? search,
        string? sortBy,
        string? sortDirection) =>
        $"{TenantRoot(tenantId)}Products_All_p{page}_s{pageSize}_c{categoryId?.ToString() ?? "all"}_q{Normalize(search)}_sb{Normalize(sortBy)}_sd{Normalize(sortDirection)}";

    public static string ProductById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Product_{id}";

    public static string ProductsByCategory(
        Guid tenantId,
        Guid categoryId,
        int page,
        int pageSize) =>
        $"{TenantRoot(tenantId)}Products_ByCategory_{categoryId}_p{page}_s{pageSize}";

    // ── Categories ────────────────────────────────────────────────────────
    public static string CategoriesPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Categor";

    public static string CategoriesAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search,
        string? sortBy,
        string? sortDirection) =>
        $"{TenantRoot(tenantId)}Categories_All_p{page}_s{pageSize}_q{Normalize(search)}_sb{Normalize(sortBy)}_sd{Normalize(sortDirection)}";

    public static string CategoryById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Category_{id}";

    // ── Customers ─────────────────────────────────────────────────────────
    public static string CustomersPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Customer";

    public static string CustomersAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search,
        string? sortBy,
        string? sortDirection,
        string? city = null,
        string? country = null) =>
        $"{TenantRoot(tenantId)}Customers_All_p{page}_s{pageSize}_q{Normalize(search)}_sb{Normalize(sortBy)}_sd{Normalize(sortDirection)}_ci{Normalize(city)}_co{Normalize(country)}";

    public static string CustomerById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Customer_{id}";

    public static string CustomerAnalytics(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Customer_{id}_Analytics";

    public static string CustomerOrders(
        Guid tenantId,
        Guid customerId,
        int page,
        int pageSize) =>
        $"{TenantRoot(tenantId)}Customer_{customerId}_Orders_p{page}_s{pageSize}";

    // ── Suppliers ─────────────────────────────────────────────────────────
    public static string SuppliersPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Supplier";

    public static string SuppliersAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search,
        string? sortBy,
        string? sortDirection) =>
        $"{TenantRoot(tenantId)}Suppliers_All_p{page}_s{pageSize}_q{Normalize(search)}_sb{Normalize(sortBy)}_sd{Normalize(sortDirection)}";

    public static string SupplierById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Supplier_{id}";

    public static string SupplierProducts(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Supplier_{id}_Products";

    public static string SupplierPurchases(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Supplier_{id}_Purchases";

    // ── Inventory ─────────────────────────────────────────────────────────
    public static string InventoryPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Inventory";

    public static string InventoryAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search) =>
        $"{TenantRoot(tenantId)}Inventory_All_p{page}_s{pageSize}_q{Normalize(search)}";

    public static string InventoryByProduct(Guid tenantId, Guid productId) =>
        $"{TenantRoot(tenantId)}Inventory_Product_{productId}";

    public static string InventoryLowStock(Guid tenantId) =>
        $"{TenantRoot(tenantId)}Inventory_LowStock";

    public static string InventoryOutOfStock(Guid tenantId) =>
        $"{TenantRoot(tenantId)}Inventory_OutOfStock";

    public static string InventoryReorder(Guid tenantId) =>
        $"{TenantRoot(tenantId)}Inventory_Reorder";

    public static string InventoryAnalytics(Guid tenantId) =>
        $"{TenantRoot(tenantId)}Inventory_Analytics";

    public static string InventoryStockTransactions(
        Guid tenantId,
        Guid? productId,
        int page,
        int pageSize) =>
        $"{TenantRoot(tenantId)}Inventory_StockTransactions_p{productId?.ToString() ?? "all"}_pg{page}_s{pageSize}";

    // ── Dashboard ─────────────────────────────────────────────────────────
    public static string DashboardPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Dashboard";

    public static string Dashboard(Guid tenantId, string suffix) =>
        $"{TenantRoot(tenantId)}Dashboard_{suffix}";

    // ── Reports ───────────────────────────────────────────────────────────
    public static string ReportsPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Report";

    public static string SalesReport(Guid tenantId, string suffix) =>
        $"{TenantRoot(tenantId)}Sales_Report_{suffix}";

    public static string PurchasesReport(Guid tenantId, string suffix) =>
        $"{TenantRoot(tenantId)}Purchases_Report_{suffix}";

    public static string Report(Guid tenantId, string reportType, string suffix) =>
        $"{TenantRoot(tenantId)}Report_{reportType}_{suffix}";

    // ── Orders ────────────────────────────────────────────────────────────
    public static string OrdersPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Order";

    public static string OrdersAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search,
        string? status) =>
        $"{TenantRoot(tenantId)}Orders_All_p{page}_s{pageSize}_q{Normalize(search)}_st{Normalize(status)}";

    public static string OrderById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Order_{id}";

    // ── Invoices ──────────────────────────────────────────────────────────
    public static string InvoicesPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Invoice";

    public static string InvoicesAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search,
        string? status) =>
        $"{TenantRoot(tenantId)}Invoices_All_p{page}_s{pageSize}_q{Normalize(search)}_st{Normalize(status)}";

    public static string InvoiceById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Invoice_{id}";

    // ── Payments ──────────────────────────────────────────────────────────
    public static string PaymentsPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Payment";

    public static string PaymentsAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search) =>
        $"{TenantRoot(tenantId)}Payments_All_p{page}_s{pageSize}_q{Normalize(search)}";

    public static string PaymentById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Payment_{id}";

    // ── Purchase orders ───────────────────────────────────────────────────
    public static string PurchaseOrdersPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}PurchaseOrder";

    public static string PurchaseOrdersAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search,
        string? status) =>
        $"{TenantRoot(tenantId)}PurchaseOrders_All_p{page}_s{pageSize}_q{Normalize(search)}_st{Normalize(status)}";

    public static string PurchaseOrderById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}PurchaseOrder_{id}";

    // ── Quotations ────────────────────────────────────────────────────────
    public static string QuotationsPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Quotation";

    public static string QuotationsAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search,
        string? status) =>
        $"{TenantRoot(tenantId)}Quotations_All_p{page}_s{pageSize}_q{Normalize(search)}_st{Normalize(status)}";

    public static string QuotationById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Quotation_{id}";

    // ── Expenses ──────────────────────────────────────────────────────────
    public static string ExpensesPrefix(Guid tenantId) => $"{TenantRoot(tenantId)}Expense";

    public static string ExpensesAll(
        Guid tenantId,
        int page,
        int pageSize,
        string? search) =>
        $"{TenantRoot(tenantId)}Expenses_All_p{page}_s{pageSize}_q{Normalize(search)}";

    public static string ExpenseById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}Expense_{id}";

    public static string ExpenseCategoriesAll(Guid tenantId) =>
        $"{TenantRoot(tenantId)}ExpenseCategories_All";

    public static string ExpenseCategoryById(Guid tenantId, Guid id) =>
        $"{TenantRoot(tenantId)}ExpenseCategory_{id}";

    public static string ExpenseAnalytics(Guid tenantId, string suffix) =>
        $"{TenantRoot(tenantId)}Expense_Analytics_{suffix}";

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Trim().ToLowerInvariant().Replace(' ', '_');
}

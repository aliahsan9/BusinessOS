using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Application.Features.Agents;

/// <summary>
/// Domain specialists behind the Navla chat orchestrator. Personas (sophia/adam/emma)
/// remain the UI identity; specialists scope tools and status messaging.
/// </summary>
public static class NavlaSpecialistKeys
{
    public const string BusinessAssistant = "business-assistant";
    public const string Inventory = "inventory";
    public const string Sales = "sales";
    public const string Orders = "orders";
    public const string Customers = "customers";
    public const string Finance = "finance";
    public const string Knowledge = "knowledge";
}

public sealed record NavlaSpecialistRoute(
    IReadOnlyList<string> SpecialistKeys,
    IReadOnlyList<AiToolName> AllowedTools,
    string DisplayLabel,
    bool IsMultiModule);

public static class NavlaSpecialistCatalog
{
    public static IReadOnlyDictionary<string, IReadOnlyList<AiToolName>> ToolsBySpecialist { get; } =
        new Dictionary<string, IReadOnlyList<AiToolName>>(StringComparer.OrdinalIgnoreCase)
        {
            [NavlaSpecialistKeys.BusinessAssistant] =
            [
                AiToolName.GetBusinessHealth,
                AiToolName.GetDailyPriorities,
                AiToolName.GetBusinessSettings,
                AiToolName.GetNotificationsSummary,
                AiToolName.GetTasks,
                AiToolName.GetProjects,
                AiToolName.ApplyOnboardingProfile,
                AiToolName.UpdateCompanyProfile,
                AiToolName.UpdateTaxDefaults
            ],
            [NavlaSpecialistKeys.Inventory] =
            [
                AiToolName.GetProducts,
                AiToolName.SearchProduct,
                AiToolName.GetInventorySummary,
                AiToolName.GetLowStock,
                AiToolName.GetOutOfStock,
                AiToolName.GetDeadStock,
                AiToolName.GetInventoryHealth,
                AiToolName.GetPurchaseRecommendations,
                AiToolName.GetStockHistory,
                AiToolName.GenerateInventoryReport,
                AiToolName.CreateProduct,
                AiToolName.UpdateProduct,
                AiToolName.DeleteProduct,
                AiToolName.AdjustInventory,
                AiToolName.ReceiveStock,
                AiToolName.CreatePurchaseOrder,
                AiToolName.CreatePurchaseOrderDraft,
                AiToolName.ApprovePurchaseOrder,
                AiToolName.ReceivePurchase,
                AiToolName.SearchSupplier,
                AiToolName.CreateSupplier,
                AiToolName.UpdateSupplier,
                AiToolName.DeleteSupplier
            ],
            [NavlaSpecialistKeys.Sales] =
            [
                AiToolName.GetSalesSummary,
                AiToolName.GetSaleDetails,
                AiToolName.GetRevenue,
                AiToolName.GetBestSellingProducts,
                AiToolName.GetSalesTrends,
                AiToolName.GenerateSalesReport,
                AiToolName.CreateSale
            ],
            [NavlaSpecialistKeys.Orders] =
            [
                AiToolName.GetOrders,
                AiToolName.GetOrderDetails,
                AiToolName.GetPendingOrders,
                AiToolName.CreateSale,
                AiToolName.UpdateOrderStatus,
                AiToolName.CreateInvoice,
                AiToolName.CancelInvoice,
                AiToolName.SearchInvoice
            ],
            [NavlaSpecialistKeys.Customers] =
            [
                AiToolName.GetCustomers,
                AiToolName.SearchCustomer,
                AiToolName.GetCustomerInsights,
                AiToolName.GetCustomerPurchaseHistory,
                AiToolName.CreateCustomer,
                AiToolName.UpdateCustomer,
                AiToolName.DeleteCustomer
            ],
            [NavlaSpecialistKeys.Finance] =
            [
                AiToolName.GetFinancialSummary,
                AiToolName.GetExpenses,
                AiToolName.GetRevenue,
                AiToolName.GetOutstandingPayments,
                AiToolName.GetInvoices,
                AiToolName.ShowProfit,
                AiToolName.SearchInvoice,
                AiToolName.CreateExpense,
                AiToolName.CreateInvoice,
                AiToolName.CancelInvoice
            ],
            [NavlaSpecialistKeys.Knowledge] =
            [
                AiToolName.SearchDocuments
            ]
        };

    public static string DisplayName(string key) => key switch
    {
        NavlaSpecialistKeys.BusinessAssistant => "Business Assistant",
        NavlaSpecialistKeys.Inventory => "Inventory Agent",
        NavlaSpecialistKeys.Sales => "Sales Agent",
        NavlaSpecialistKeys.Orders => "Order Agent",
        NavlaSpecialistKeys.Customers => "Customer Agent",
        NavlaSpecialistKeys.Finance => "Finance Agent",
        NavlaSpecialistKeys.Knowledge => "Knowledge Agent",
        _ => "Navla"
    };
}

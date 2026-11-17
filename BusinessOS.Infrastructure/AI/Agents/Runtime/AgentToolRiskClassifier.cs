using BusinessOS.Application.Features.AI.Enums;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

/// <summary>
/// Classifies tools by their potential business impact. Unknown or newly added tools
/// default to Critical so a new capability cannot silently inherit a permissive policy.
/// </summary>
public static class AgentToolRiskClassifier
{
    public static AgentToolRiskLevel Classify(AiToolName tool) => tool switch
    {
        // Queries, search, reports, recommendations and analytics do not mutate business records.
        AiToolName.GetCustomers or AiToolName.GetProjects or AiToolName.GetTasks
            or AiToolName.GetInvoices or AiToolName.GetExpenses or AiToolName.GetProducts
            or AiToolName.GetRevenue or AiToolName.GetSalesSummary
            or AiToolName.GetBestSellingProducts or AiToolName.GetSalesTrends
            or AiToolName.SearchDocuments or AiToolName.GetInventorySummary
            or AiToolName.GetLowStock or AiToolName.GetDeadStock
            or AiToolName.GenerateInventoryReport or AiToolName.GenerateSalesReport
            or AiToolName.GetPurchaseRecommendations or AiToolName.GetBusinessSettings
            or AiToolName.GetNotificationsSummary or AiToolName.SearchCustomer
            or AiToolName.SearchProduct or AiToolName.SearchInvoice
            or AiToolName.SearchSupplier or AiToolName.ShowProfit
            or AiToolName.GetOutOfStock or AiToolName.GetInventoryHealth
            or AiToolName.GetBusinessHealth or AiToolName.GetDailyPriorities
            or AiToolName.GetCustomerInsights
            => AgentToolRiskLevel.ReadOnly,

        // Low-impact operational work: creates a task/project without moving money or stock.
        AiToolName.CreateTask or AiToolName.CreateProject
            => AgentToolRiskLevel.Low,

        // Master-data and company configuration changes can disrupt business operations.
        AiToolName.CreateCustomer or AiToolName.UpdateCustomer or AiToolName.DeleteCustomer
            or AiToolName.CreateProduct or AiToolName.UpdateProduct or AiToolName.DeleteProduct
            or AiToolName.CreateSupplier or AiToolName.UpdateSupplier or AiToolName.DeleteSupplier
            or AiToolName.UpdateCompanyProfile or AiToolName.ApplyOnboardingProfile
            or AiToolName.CreatePurchaseOrderDraft
            => AgentToolRiskLevel.High,

        // Financial, tax, inventory and purchasing operations have direct monetary impact.
        AiToolName.CreateInvoice or AiToolName.CancelInvoice or AiToolName.CreateSale
            or AiToolName.AdjustInventory or AiToolName.ReceiveStock
            or AiToolName.CreatePurchaseOrder or AiToolName.ApprovePurchaseOrder
            or AiToolName.ReceivePurchase or AiToolName.UpdateTaxDefaults
            => AgentToolRiskLevel.Critical,

        // Fail closed when an enum member is added but not classified above.
        _ => AgentToolRiskLevel.Critical
    };

    /// <summary>
    /// Current safe-default policy: every write requires approval. Future limited-autonomy
    /// policy may allow Low-risk actions only after tenant-level opt-in and server enforcement.
    /// </summary>
    public static bool RequiresExplicitApproval(AiToolName tool) =>
        Classify(tool) != AgentToolRiskLevel.ReadOnly;
}

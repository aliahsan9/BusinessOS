namespace BusinessOS.Application.Features.AI.Enums;

public enum AiCopilotIntent
{
    Unknown = 0,
    Conversational = 1,
    Help = 2,
    BusinessIntelligence = 3,
    Analytics = 4,
    DocumentSearch = 5,
    ActionCreate = 6,
    ActionRead = 7,
    DashboardInsight = 8,
    FollowUp = 9,
    Onboarding = 10,
    ReportGeneration = 11,
    Workflow = 12,
    Recommendation = 13
}

public enum AiToolName
{
    GetCustomers,
    GetProjects,
    GetTasks,
    GetInvoices,
    GetExpenses,
    GetProducts,
    GetRevenue,
    GetSalesSummary,
    GetBestSellingProducts,
    GetSalesTrends,
    CreateTask,
    CreateInvoice,
    CreateCustomer,
    CreateProject,
    SearchDocuments,
    GetInventorySummary,
    GetLowStock,
    GetDeadStock,
    CreatePurchaseOrderDraft,
    GenerateInventoryReport,
    GenerateSalesReport,
    GetPurchaseRecommendations,
    ApplyOnboardingProfile,
    GetBusinessSettings,
    GetNotificationsSummary
}

using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Billing;

public static class SubscriptionCatalog
{
    public const int Revision = 2;
    public const string BaseCurrency = "USD";

    public static readonly Guid FreePlanId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid StarterPlanId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ProfessionalPlanId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid EnterprisePlanId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static IReadOnlyList<SubscriptionPlan> CreatePlans() =>
    [
        Plan(FreePlanId, "Free", "free", "Get started with basic business management", 0, 0, 1, 25, 50, 3, 100, 512, 20,
            ai: true, analytics: false, pdf: false, reports: false, priority: false, api: false, branches: false,
            inventory: "Basic", sales: "Limited", trialDays: 0, custom: false, sort: 0),
        Plan(StarterPlanId, "Starter", "starter", "For growing small businesses", 19, 182, 3, 500, 1000, 25, 2000, 5120, 100,
            ai: true, analytics: false, pdf: true, reports: false, priority: false, api: false, branches: false,
            inventory: "Full", sales: "Full", trialDays: 14, custom: false, sort: 1),
        Plan(ProfessionalPlanId, "Professional", "professional", "Advanced tools for professional teams", 49, 470, 10, 5000, 10000, 200, 10000, 51200, 500,
            ai: true, analytics: true, pdf: true, reports: true, priority: false, api: true, branches: false,
            inventory: "Full", sales: "Full", trialDays: 14, custom: false, sort: 2),
        Plan(EnterprisePlanId, "Enterprise", "enterprise", "Custom contracts, unlimited scale, and priority support", 149, 1430, SubscriptionPlan.Unlimited, SubscriptionPlan.Unlimited, SubscriptionPlan.Unlimited, SubscriptionPlan.Unlimited, SubscriptionPlan.Unlimited, SubscriptionPlan.Unlimited, SubscriptionPlan.Unlimited,
            ai: true, analytics: true, pdf: true, reports: true, priority: true, api: true, branches: true,
            inventory: "Full", sales: "Full", trialDays: 14, custom: true, sort: 3)
    ];

    public static bool ShouldPreserveContract(SubscriptionStatus status, decimal monthlyPrice) =>
        monthlyPrice > 0 && status is SubscriptionStatus.Trial
            or SubscriptionStatus.Active
            or SubscriptionStatus.PastDue
            or SubscriptionStatus.PendingPayment
            or SubscriptionStatus.Incomplete;

    public static ContractedEntitlement Snapshot(SubscriptionPlan plan) => new()
    {
        MonthlyPrice = plan.MonthlyPrice,
        AnnualPrice = plan.AnnualPrice,
        Currency = string.IsNullOrWhiteSpace(plan.BaseCurrency) ? BaseCurrency : plan.BaseCurrency,
        MaxUsers = plan.MaxUsers,
        MaxCustomers = plan.MaxCustomers,
        MaxProducts = plan.MaxProducts,
        MaxProjects = plan.MaxProjects,
        MaxTasks = plan.MaxTasks,
        MaxStorageMb = plan.MaxStorageMb,
        MaxAiRequests = plan.MaxAiRequests,
        HasAiAssistant = plan.HasAiAssistant,
        HasAdvancedAnalytics = plan.HasAdvancedAnalytics,
        HasPdfReports = plan.HasPdfReports,
        HasAdvancedReports = plan.HasAdvancedReports,
        HasPrioritySupport = plan.HasPrioritySupport,
        HasApiAccess = plan.HasApiAccess,
        HasMultiBranch = plan.HasMultiBranch,
        InventoryAccess = plan.InventoryAccess,
        SalesAccess = plan.SalesAccess
    };

    public static void CopyCatalog(SubscriptionPlan target, SubscriptionPlan source)
    {
        target.Name = source.Name;
        target.Slug = source.Slug;
        target.Description = source.Description;
        target.MonthlyPrice = source.MonthlyPrice;
        target.AnnualPrice = source.AnnualPrice;
        target.MaxUsers = source.MaxUsers;
        target.MaxCustomers = source.MaxCustomers;
        target.MaxProducts = source.MaxProducts;
        target.MaxProjects = source.MaxProjects;
        target.MaxTasks = source.MaxTasks;
        target.MaxStorageMb = source.MaxStorageMb;
        target.MaxAiRequests = source.MaxAiRequests;
        target.HasAiAssistant = source.HasAiAssistant;
        target.HasAdvancedAnalytics = source.HasAdvancedAnalytics;
        target.HasPdfReports = source.HasPdfReports;
        target.HasAdvancedReports = source.HasAdvancedReports;
        target.HasPrioritySupport = source.HasPrioritySupport;
        target.HasApiAccess = source.HasApiAccess;
        target.HasMultiBranch = source.HasMultiBranch;
        target.InventoryAccess = source.InventoryAccess;
        target.SalesAccess = source.SalesAccess;
        target.TrialDays = source.TrialDays;
        target.BaseCurrency = source.BaseCurrency;
        target.IsCustomPricing = source.IsCustomPricing;
        target.IsPublic = source.IsPublic;
        target.SortOrder = source.SortOrder;
        target.IsActive = source.IsActive;
        target.CatalogRevision = Revision;
    }

    public static IReadOnlyList<string> BuildFeatures(SubscriptionPlan plan)
    {
        var features = new List<string>();
        if (plan.IsCustomPricing)
        {
            features.Add("Custom users");
            features.Add("Unlimited customers");
            features.Add("Unlimited products");
            features.Add("Unlimited projects");
            features.Add("Unlimited tasks");
        }
        else
        {
            features.Add($"{Format(plan.MaxUsers)} users");
            features.Add($"{Format(plan.MaxCustomers)} customers");
            features.Add($"{Format(plan.MaxProducts)} products");
            features.Add($"{Format(plan.MaxProjects)} projects");
            features.Add($"{Format(plan.MaxTasks)} tasks");
        }

        features.Add(plan.InventoryAccess == "Full" ? "Full inventory" : "Basic inventory");
        features.Add(plan.SalesAccess == "Full" ? "Full sales and orders" : "Limited sales and orders");
        features.Add(plan.HasAdvancedAnalytics ? "Advanced analytics" : "Basic analytics");
        if (plan.HasPdfReports) features.Add("PDF reports");
        features.Add(plan.IsCustomPricing ? "Custom AI requests" : $"{Format(plan.MaxAiRequests)} AI requests / month");
        if (plan.HasApiAccess) features.Add("API access");
        if (plan.HasMultiBranch) features.Add("Multi-branch");
        if (plan.HasPrioritySupport) features.Add("Priority support");
        if (plan.TrialDays > 0) features.Add($"{plan.TrialDays}-day free trial");
        return features;
    }

    private static string Format(int limit) => limit == SubscriptionPlan.Unlimited ? "Unlimited" : limit.ToString("N0");

    private static SubscriptionPlan Plan(
        Guid id,
        string name,
        string slug,
        string description,
        decimal monthly,
        decimal annual,
        int users,
        int customers,
        int products,
        int projects,
        int tasks,
        long storage,
        int aiRequests,
        bool ai,
        bool analytics,
        bool pdf,
        bool reports,
        bool priority,
        bool api,
        bool branches,
        string inventory,
        string sales,
        int trialDays,
        bool custom,
        int sort) => new()
    {
        Id = id,
        Name = name,
        Slug = slug,
        Description = description,
        MonthlyPrice = monthly,
        AnnualPrice = annual,
        MaxUsers = users,
        MaxCustomers = customers,
        MaxProducts = products,
        MaxProjects = projects,
        MaxTasks = tasks,
        MaxStorageMb = storage,
        MaxAiRequests = aiRequests,
        HasAiAssistant = ai,
        HasAdvancedAnalytics = analytics,
        HasPdfReports = pdf,
        HasAdvancedReports = reports,
        HasPrioritySupport = priority,
        HasApiAccess = api,
        HasMultiBranch = branches,
        InventoryAccess = inventory,
        SalesAccess = sales,
        TrialDays = trialDays,
        BaseCurrency = BaseCurrency,
        IsCustomPricing = custom,
        IsPublic = true,
        IsActive = true,
        SortOrder = sort,
        CatalogRevision = Revision,
        CreatedAt = DateTime.UtcNow
    };
}

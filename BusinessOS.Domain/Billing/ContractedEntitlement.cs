namespace BusinessOS.Domain.Billing;

/// <summary>
/// Limits and prices locked for an existing subscriber when the public catalog changes.
/// </summary>
public sealed class ContractedEntitlement
{
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public int MaxUsers { get; set; }
    public int MaxCustomers { get; set; }
    public int MaxProducts { get; set; }
    public int MaxProjects { get; set; }
    public int MaxTasks { get; set; }
    public long MaxStorageMb { get; set; }
    public int MaxAiRequests { get; set; }
    public bool HasAiAssistant { get; set; }
    public bool HasAdvancedAnalytics { get; set; }
    public bool HasPdfReports { get; set; }
    public bool HasAdvancedReports { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool HasApiAccess { get; set; }
    public bool HasMultiBranch { get; set; }
    public string InventoryAccess { get; set; } = "Basic";
    public string SalesAccess { get; set; } = "Limited";
}

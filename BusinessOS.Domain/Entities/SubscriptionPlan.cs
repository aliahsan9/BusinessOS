using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class SubscriptionPlan : BaseEntity
{
    public const int Unlimited = -1;

    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int MaxUsers { get; set; } = 1;
    public int MaxCustomers { get; set; } = 25;
    public int MaxProjects { get; set; } = 10;
    public int MaxTasks { get; set; } = 100;
    public long MaxStorageMb { get; set; } = 512;
    public int MaxAiRequests { get; set; }
    public bool HasAiAssistant { get; set; }
    public bool HasAdvancedAnalytics { get; set; }
    public bool HasPdfReports { get; set; }
    public bool HasAdvancedReports { get; set; }
    public bool HasPrioritySupport { get; set; }
    public string? StripePriceIdMonthly { get; set; }
    public string? StripePriceIdYearly { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static bool IsUnlimited(int limit) => limit == Unlimited;
}

namespace BusinessOS.Application.Features.Billing.DTOs;

public class CommercialPlanUpdateRequest
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
    public int MaxAgentExecutions { get; set; }
    public int MaxAutomationRuns { get; set; }
    public int GracePeriodDays { get; set; }
    public int TrialDays { get; set; }
}

public class FeatureGrantRequest
{
    public bool Enabled { get; set; }
    public long? Limit { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Reason { get; set; }
}

public class GrantTrialRequest
{
    public Guid PlanId { get; set; }
    public int Days { get; set; } = 14;
}

public class ModelPriceRequest
{
    public string Model { get; set; } = default!;
    public string UsageCategory { get; set; } = "chat";
    public decimal InputUsdPerMillion { get; set; }
    public decimal OutputUsdPerMillion { get; set; }
    public string? Provider { get; set; }
}

public class FailedPaymentDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Provider { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ModelPriceDto
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = default!;
    public string Model { get; set; } = default!;
    public string UsageCategory { get; set; } = default!;
    public decimal InputUsdPerMillion { get; set; }
    public decimal OutputUsdPerMillion { get; set; }
    public string PricingVersion { get; set; } = default!;
    public DateTime EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
}

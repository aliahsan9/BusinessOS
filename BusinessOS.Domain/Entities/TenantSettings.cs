using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class TenantSettings : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string Currency { get; set; } = "USD";
    public string Language { get; set; } = "en";
    public decimal TaxRate { get; set; }
    public string? InvoicePrefix { get; set; }
    public string? EmailFromAddress { get; set; }
    public string Theme { get; set; } = "light";
    public string? LogoUrl { get; set; }
    public string Timezone { get; set; } = "UTC";
    /// <summary>Calendar month (1-12) when the financial year starts. Pakistan default is July (7).</summary>
    public int FinancialYearStartMonth { get; set; } = 7;
    public bool AiAssistantEnabled { get; set; } = true;
    public bool AiShowSuggestions { get; set; } = true;
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool SystemNotificationsEnabled { get; set; } = true;
    public bool OrderNotificationsEnabled { get; set; } = true;
    public bool InventoryAlertsEnabled { get; set; } = true;
    public bool PaymentAlertsEnabled { get; set; } = true;
    public bool TaskNotificationsEnabled { get; set; } = true;
    public bool InvoiceNotificationsEnabled { get; set; } = true;
    public bool CustomerNotificationsEnabled { get; set; } = true;
    public bool ProjectNotificationsEnabled { get; set; } = true;
    public TimeOnly WorkdayStart { get; set; } = new(9, 0);
    public TimeOnly WorkdayEnd { get; set; } = new(17, 0);
    public int LateGraceMinutes { get; set; } = 15;
    public bool AllowNegativeStock { get; set; }
    public decimal OpeningCashBalance { get; set; }
    public bool RequireMfa { get; set; }
    public int AuditRetentionDays { get; set; } = 365;
    public int SessionIdleMinutes { get; set; } = 60;
    public int MaxConcurrentSessions { get; set; } = 10;

    public Tenant Tenant { get; set; } = default!;
}

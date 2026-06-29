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

    public Tenant Tenant { get; set; } = default!;
}

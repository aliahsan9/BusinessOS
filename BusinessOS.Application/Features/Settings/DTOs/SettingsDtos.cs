namespace BusinessOS.Application.Features.Settings.DTOs;

public class TenantSettingsDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Currency { get; set; } = "USD";
    public string Language { get; set; } = "en";
    public decimal TaxRate { get; set; }
    public string? InvoicePrefix { get; set; }
    public string? EmailFromAddress { get; set; }
    public string Theme { get; set; } = "light";
    public string? LogoUrl { get; set; }
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool SystemNotificationsEnabled { get; set; } = true;
    public bool OrderNotificationsEnabled { get; set; } = true;
    public bool InventoryAlertsEnabled { get; set; } = true;
    public bool PaymentAlertsEnabled { get; set; } = true;
    public bool TaskNotificationsEnabled { get; set; } = true;
    public bool InvoiceNotificationsEnabled { get; set; } = true;
    public bool CustomerNotificationsEnabled { get; set; } = true;
    public bool ProjectNotificationsEnabled { get; set; } = true;
}

public class BusinessProfileDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string BusinessType { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string SubscriptionPlan { get; set; } = default!;
    public bool IsActive { get; set; }
    public TenantSettingsDto Settings { get; set; } = default!;
}

public record UpdateTenantSettingsRequest(
    string Currency,
    string Language,
    decimal TaxRate,
    string? InvoicePrefix,
    string? EmailFromAddress,
    string Theme,
    string? LogoUrl,
    bool EmailNotificationsEnabled,
    bool SystemNotificationsEnabled,
    bool OrderNotificationsEnabled,
    bool InventoryAlertsEnabled,
    bool PaymentAlertsEnabled,
    bool TaskNotificationsEnabled,
    bool InvoiceNotificationsEnabled,
    bool CustomerNotificationsEnabled,
    bool ProjectNotificationsEnabled);

public record UpdateBusinessProfileRequest(
    string Name,
    string BusinessType,
    string Email,
    string Phone,
    string Address);

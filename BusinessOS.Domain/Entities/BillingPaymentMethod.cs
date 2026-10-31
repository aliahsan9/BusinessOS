using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Tokenized payment-method summary. Raw card numbers and CVV are never stored.
/// </summary>
public class BillingPaymentMethod : AuditableEntity
{
    public Guid TenantId { get; set; }
    public PaymentProviderType Provider { get; set; }
    public string? ExternalId { get; set; }
    public string Brand { get; set; } = "card";
    public string Last4 { get; set; } = "";
    public int? ExpMonth { get; set; }
    public int? ExpYear { get; set; }
    public bool IsDefault { get; set; }
    public bool SupportsRecurring { get; set; }

    public Tenant Tenant { get; set; } = default!;
}

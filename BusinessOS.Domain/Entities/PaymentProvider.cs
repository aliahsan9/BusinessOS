using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

public class PaymentProvider : BaseEntity
{
    public PaymentProviderType ProviderType { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsSandbox { get; set; } = true;
    public string? ConfigurationKey { get; set; }
    public string? MerchantCountry { get; set; }
    public string? SupportedCurrencies { get; set; }
    public bool SupportsOneTime { get; set; } = true;
    public bool SupportsRecurring { get; set; }
    public bool SupportsRefunds { get; set; }
    public bool SupportsTokenization { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

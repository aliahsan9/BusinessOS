using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Server-configured price for a plan in a billing country and currency.
/// Amounts are not derived from a live exchange rate.
/// </summary>
public class RegionalPrice : BaseEntity
{
    public Guid SubscriptionPlanId { get; set; }
    public string CountryCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "USD";
    public decimal MonthlyAmount { get; set; }
    public decimal AnnualAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ProviderPriceIdMonthly { get; set; }
    public string? ProviderPriceIdAnnual { get; set; }
    public string? ExchangeRateNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SubscriptionPlan Plan { get; set; } = default!;
}

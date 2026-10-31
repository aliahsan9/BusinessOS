namespace BusinessOS.Domain.Billing;

public sealed record PaymentMethodCapability(
    string Code,
    string Name,
    string Region,
    bool OneTime,
    bool Recurring,
    bool CustomerInitiatedRenewal,
    bool Refunds,
    bool Tokenization,
    IReadOnlyList<string> Currencies,
    IReadOnlyList<string> Providers);

public static class PaymentMethodCatalog
{
    public static readonly IReadOnlyList<PaymentMethodCapability> All =
    [
        new("card", "Visa and Mastercard", "Global", true, true, true, true, true,
            ["USD", "AED", "SAR", "QAR", "KWD", "BHD", "OMR", "GBP", "EUR", "CAD", "AUD", "SGD"],
            ["stripe", "tap", "paytabs"]),
        new("amex", "American Express", "Global", true, true, true, true, true,
            ["USD", "AED", "GBP", "EUR", "CAD", "AUD", "SGD"],
            ["stripe", "tap", "paytabs"]),
        new("apple_pay", "Apple Pay", "Global", true, true, true, true, true,
            ["USD", "AED", "SAR", "GBP", "EUR", "CAD", "AUD", "SGD"],
            ["stripe", "tap", "paytabs"]),
        new("google_pay", "Google Pay", "Global", true, true, true, true, true,
            ["USD", "AED", "SAR", "GBP", "EUR", "CAD", "AUD", "SGD"],
            ["stripe", "tap", "paytabs"]),
        new("link", "Link", "Global", true, true, true, true, true,
            ["USD", "GBP", "EUR", "CAD", "AUD", "SGD"],
            ["stripe"]),
        new("mada", "Saudi Mada", "GCC", true, false, true, true, false,
            ["SAR"],
            ["tap", "paytabs"]),
        new("stc_pay", "STC Pay", "GCC", true, false, true, false, false,
            ["SAR"],
            ["paytabs"]),
        new("knet", "KNET", "GCC", true, false, false, true, false,
            ["KWD"],
            ["tap"]),
        new("benefit", "Benefit", "GCC", true, false, false, true, false,
            ["BHD"],
            ["tap", "paytabs"]),
        new("omannet", "OmanNet", "GCC", true, false, false, false, false,
            ["OMR"],
            ["tap"]),
        new("qpay", "QPay", "GCC", true, false, false, false, false,
            ["QAR"],
            ["paytabs"])
    ];

    public static IReadOnlyList<PaymentMethodCapability> For(string currency, IReadOnlySet<string> enabledProviders)
    {
        var code = currency.ToUpperInvariant();
        return All
            .Where(method =>
                method.Currencies.Contains(code, StringComparer.OrdinalIgnoreCase) &&
                method.Providers.Any(provider => enabledProviders.Contains(provider)))
            .Select(method => method with
            {
                Providers = method.Providers.Where(provider => enabledProviders.Contains(provider)).ToArray(),
                Recurring = method.Recurring && method.Providers.Any(provider =>
                    enabledProviders.Contains(provider) && provider is "stripe" or "tap")
            })
            .ToArray();
    }
}

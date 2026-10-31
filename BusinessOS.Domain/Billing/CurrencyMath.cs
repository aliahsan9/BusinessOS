namespace BusinessOS.Domain.Billing;

/// <summary>
/// Decimal money helpers. Financial amounts are never calculated with floating-point types.
/// </summary>
public static class CurrencyMath
{
    public static readonly IReadOnlySet<string> SupportedCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "AED", "SAR", "QAR", "KWD", "BHD", "OMR", "GBP", "EUR", "CAD", "AUD", "SGD", "PKR"
    };

    public static int Scale(string? currency)
    {
        var code = Normalize(currency);
        return code switch
        {
            "KWD" or "BHD" or "OMR" => 3,
            "JPY" or "KRW" => 0,
            _ => 2
        };
    }

    public static string Normalize(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return "USD";

        var code = currency.Trim().ToUpperInvariant();
        if (code.Length != 3 || !SupportedCurrencies.Contains(code))
            throw new ArgumentException($"Unsupported currency '{currency}'.", nameof(currency));

        return code;
    }

    public static bool TryNormalize(string? currency, out string code)
    {
        code = "USD";
        if (string.IsNullOrWhiteSpace(currency))
            return true;

        var candidate = currency.Trim().ToUpperInvariant();
        if (candidate.Length != 3 || !SupportedCurrencies.Contains(candidate))
            return false;

        code = candidate;
        return true;
    }

    public static decimal Round(decimal amount, string? currency) =>
        decimal.Round(amount, Scale(currency), MidpointRounding.AwayFromZero);

    public static long ToMinorUnits(decimal amount, string? currency)
    {
        var rounded = Round(amount, currency);
        return (long)decimal.Round(rounded * Factor(Scale(currency)), 0, MidpointRounding.AwayFromZero);
    }

    public static decimal FromMinorUnits(long minorUnits, string? currency) =>
        minorUnits / Factor(Scale(currency));

    private static decimal Factor(int scale) => scale switch
    {
        0 => 1m,
        1 => 10m,
        3 => 1000m,
        _ => 100m
    };
}

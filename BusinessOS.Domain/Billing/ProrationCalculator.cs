namespace BusinessOS.Domain.Billing;

public static class ProrationCalculator
{
    public static decimal EstimateAmountDue(
        decimal currentPeriodPrice,
        decimal newPeriodPrice,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime utcNow,
        string currency)
    {
        var totalTicks = periodEndUtc.Ticks - periodStartUtc.Ticks;
        if (totalTicks <= 0)
            return CurrencyMath.Round(newPeriodPrice, currency);

        var remainingTicks = periodEndUtc.Ticks - utcNow.Ticks;
        if (remainingTicks < 0)
            remainingTicks = 0;
        if (remainingTicks > totalTicks)
            remainingTicks = totalTicks;

        var fraction = (decimal)remainingTicks / totalTicks;
        var credit = currentPeriodPrice * fraction;
        var charge = newPeriodPrice * fraction;
        var due = charge - credit;
        if (due < 0)
            due = 0;

        return CurrencyMath.Round(due, currency);
    }
}

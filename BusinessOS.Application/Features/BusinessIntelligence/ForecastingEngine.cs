namespace BusinessOS.Application.Features.BusinessIntelligence;

/// <summary>
/// Pure statistical forecasting helpers. No ML models — linear trend and moving averages only.
/// Predictions are estimates; callers must surface confidence, assumptions, and <see cref="ForecastSeriesResult.IsPrediction"/>.
/// </summary>
public static class ForecastingEngine
{
    public const int MinimumPointsForForecast = 3;
    public const int MediumConfidencePoints = 6;
    public const int HighConfidencePoints = 12;

    public static ForecastSeriesResult Forecast(
        IReadOnlyList<decimal> history,
        int horizon = 1,
        string seriesLabel = "value")
    {
        horizon = Math.Clamp(horizon, 1, 12);
        if (history.Count < MinimumPointsForForecast || history.All(x => x == 0m))
        {
            return new ForecastSeriesResult(
                Available: false,
                Confidence: "unavailable",
                Method: "none",
                Points: [],
                MeanAbsolutePercentageError: null,
                Assumptions:
                [
                    $"At least {MinimumPointsForForecast} non-empty historical periods are required before a forecast is offered.",
                    "No invented values were generated."
                ],
                Limitations: [$"Insufficient history for {seriesLabel} ({history.Count} period(s))."],
                IsPrediction: true);
        }

        var sma = SimpleMovingAverage(history, Math.Min(3, history.Count));
        var (trendNext, slope, rSquared) = LinearTrendNext(history);
        var useTrend = rSquared >= 0.35m && Math.Abs(slope) > 0.01m * Average(history);
        var method = useTrend ? "linear-trend" : "simple-moving-average";
        var mape = BacktestMape(history, useTrend);

        var points = new List<ForecastPoint>();
        var last = history[^1];
        for (var i = 1; i <= horizon; i++)
        {
            decimal value;
            string pointConfidence;
            if (useTrend)
            {
                var n = history.Count;
                value = Math.Max(0m, decimal.Round(trendNext + slope * (i - 1), 2));
                pointConfidence = ConfidenceFor(history.Count, mape, i);
            }
            else
            {
                value = decimal.Round(sma, 2);
                // Mild mean-reversion toward SMA from last actual for multi-step.
                value = decimal.Round(last + (sma - last) * (i / (decimal)(horizon + 1)), 2);
                if (value < 0) value = 0;
                pointConfidence = ConfidenceFor(history.Count, mape, i);
                last = value;
            }

            points.Add(new ForecastPoint(i, value, pointConfidence));
        }

        var overall = ConfidenceFor(history.Count, mape, 1);
        return new ForecastSeriesResult(
            Available: true,
            Confidence: overall,
            Method: method,
            Points: points,
            MeanAbsolutePercentageError: mape,
            Assumptions:
            [
                useTrend
                    ? "Linear least-squares trend fitted to completed historical periods; future periods continue that slope."
                    : "Simple moving average of recent periods; no seasonality model is applied.",
                "Forecasts are estimates derived from recorded history only — not guaranteed outcomes.",
                $"Series: {seriesLabel}."
            ],
            Limitations: BuildLimitations(history.Count, mape, useTrend, rSquared),
            IsPrediction: true);
    }

    public static string ConfidenceFor(int historyCount, decimal? mape, int stepAhead)
    {
        if (historyCount < MinimumPointsForForecast)
            return "unavailable";

        var baseLevel = historyCount >= HighConfidencePoints ? 2
            : historyCount >= MediumConfidencePoints ? 1
            : 0;

        if (mape is >= 40m)
            baseLevel = Math.Max(0, baseLevel - 1);
        else if (mape is <= 15m && historyCount >= MediumConfidencePoints)
            baseLevel = Math.Min(2, baseLevel + 0);

        if (stepAhead > 3)
            baseLevel = Math.Max(0, baseLevel - 1);
        if (stepAhead > 6)
            baseLevel = 0;

        return baseLevel switch
        {
            2 => "high",
            1 => "medium",
            _ => "low"
        };
    }

    public static decimal SimpleMovingAverage(IReadOnlyList<decimal> history, int window)
    {
        window = Math.Clamp(window, 1, history.Count);
        var slice = history.Skip(history.Count - window).Take(window);
        return Average(slice.ToList());
    }

    public static (decimal Next, decimal Slope, decimal RSquared) LinearTrendNext(IReadOnlyList<decimal> history)
    {
        var n = history.Count;
        if (n < 2)
            return (history.Count == 0 ? 0m : history[0], 0m, 0m);

        decimal sumX = 0, sumY = 0, sumXY = 0, sumXX = 0, sumYY = 0;
        for (var i = 0; i < n; i++)
        {
            var x = i + 1m;
            var y = history[i];
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumXX += x * x;
            sumYY += y * y;
        }

        var denom = n * sumXX - sumX * sumX;
        if (denom == 0)
            return (history[^1], 0m, 0m);

        var slope = (n * sumXY - sumX * sumY) / denom;
        var intercept = (sumY - slope * sumX) / n;
        var next = intercept + slope * (n + 1);

        var ssTot = sumYY - (sumY * sumY) / n;
        decimal ssRes = 0;
        for (var i = 0; i < n; i++)
        {
            var fitted = intercept + slope * (i + 1);
            var err = history[i] - fitted;
            ssRes += err * err;
        }

        var r2 = ssTot == 0 ? 0m : decimal.Round(1m - (ssRes / ssTot), 4);
        if (r2 < 0) r2 = 0;
        return (decimal.Round(next, 4), decimal.Round(slope, 4), r2);
    }

    /// <summary>One-step-ahead MAPE using leave-last-out when enough history exists.</summary>
    public static decimal? BacktestMape(IReadOnlyList<decimal> history, bool useTrend)
    {
        if (history.Count < MinimumPointsForForecast + 1)
            return null;

        var train = history.Take(history.Count - 1).ToList();
        var actual = history[^1];
        if (actual == 0m)
            return null;

        decimal predicted;
        if (useTrend)
            predicted = Math.Max(0m, LinearTrendNext(train).Next);
        else
            predicted = SimpleMovingAverage(train, Math.Min(3, train.Count));

        var ape = Math.Abs(actual - predicted) / Math.Abs(actual) * 100m;
        return decimal.Round(ape, 2);
    }

    private static decimal Average(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0) return 0m;
        decimal sum = 0;
        foreach (var v in values) sum += v;
        return sum / values.Count;
    }

    private static List<string> BuildLimitations(int count, decimal? mape, bool useTrend, decimal rSquared)
    {
        var list = new List<string>();
        if (count < HighConfidencePoints)
            list.Add($"Only {count} historical period(s); confidence is reduced until at least {HighConfidencePoints} periods exist.");
        if (mape is >= 25m)
            list.Add($"Recent one-step backtest MAPE was {mape:0.##}%; treat projections cautiously.");
        if (useTrend && rSquared < 0.5m)
            list.Add($"Trend fit is moderate (R²={rSquared:0.##}); slope may be unstable.");
        if (!useTrend)
            list.Add("No strong linear trend detected; moving-average projection assumes recent averages persist.");
        list.Add("Seasonality, promotions, and external shocks are not modeled.");
        return list;
    }
}

public sealed record ForecastPoint(int StepAhead, decimal Value, string Confidence);

public sealed record ForecastSeriesResult(
    bool Available,
    string Confidence,
    string Method,
    IReadOnlyList<ForecastPoint> Points,
    decimal? MeanAbsolutePercentageError,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations,
    bool IsPrediction);

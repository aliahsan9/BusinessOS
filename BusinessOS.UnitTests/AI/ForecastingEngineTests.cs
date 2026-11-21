using BusinessOS.Application.Features.BusinessIntelligence;
using FluentAssertions;

namespace BusinessOS.UnitTests.AI;

public class ForecastingEngineTests
{
    [Fact]
    public void Forecast_WithInsufficientHistory_IsUnavailable()
    {
        var result = ForecastingEngine.Forecast([10m, 12m], horizon: 1);

        result.Available.Should().BeFalse();
        result.Confidence.Should().Be("unavailable");
        result.IsPrediction.Should().BeTrue();
        result.Points.Should().BeEmpty();
        result.Limitations.Should().Contain(x => x.Contains("Insufficient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Forecast_WithAllZeros_IsUnavailable()
    {
        var result = ForecastingEngine.Forecast([0m, 0m, 0m, 0m], horizon: 1);
        result.Available.Should().BeFalse();
        result.Confidence.Should().Be("unavailable");
    }

    [Fact]
    public void Forecast_WithRisingSeries_ProducesPositivePrediction()
    {
        var history = Enumerable.Range(1, 8).Select(i => i * 100m).ToList();
        var result = ForecastingEngine.Forecast(history, horizon: 2, seriesLabel: "revenue");

        result.Available.Should().BeTrue();
        result.IsPrediction.Should().BeTrue();
        result.Points.Should().HaveCount(2);
        result.Points[0].Value.Should().BeGreaterThan(0);
        result.Confidence.Should().BeOneOf("low", "medium", "high");
        result.Assumptions.Should().NotBeEmpty();
        result.Method.Should().BeOneOf("linear-trend", "simple-moving-average");
    }

    [Fact]
    public void LinearTrendNext_FitsKnownLine()
    {
        var history = new List<decimal> { 10, 20, 30, 40 };
        var (next, slope, r2) = ForecastingEngine.LinearTrendNext(history);

        slope.Should().BeApproximately(10m, 0.01m);
        next.Should().BeApproximately(50m, 0.1m);
        r2.Should().BeGreaterThan(0.99m);
    }

    [Fact]
    public void ConfidenceFor_DegradesWithHorizon()
    {
        var near = ForecastingEngine.ConfidenceFor(12, 10m, 1);
        var far = ForecastingEngine.ConfidenceFor(12, 10m, 8);
        near.Should().Be("high");
        far.Should().BeOneOf("low", "medium");
    }
}

namespace BusinessOS.Application.Features.Dashboard.DTOs;

/// <summary>Chart payload optimized for line, bar, pie, and doughnut charts.</summary>
public sealed class ChartDataResponse
{
    public string ChartType { get; init; } = default!;
    public string Title { get; init; } = default!;
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<ChartDatasetDto> Datasets { get; init; } = [];
    public DashboardDateRangeInfo DateRange { get; init; } = default!;
}

public sealed class ChartDatasetDto
{
    public string Label { get; init; } = default!;
    public IReadOnlyList<decimal> Data { get; init; } = [];
    public string ChartStyle { get; init; } = "line";
}

public static class ChartTypes
{
    public const string Revenue = "revenue";
    public const string Orders = "orders";
    public const string Customers = "customers";
    public const string Products = "products";
    public const string Inventory = "inventory";
}

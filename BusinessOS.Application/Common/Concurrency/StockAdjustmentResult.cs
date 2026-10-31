namespace BusinessOS.Application.Common.Concurrency;

public readonly record struct StockAdjustmentResult(
    bool Applied,
    bool ProductMissing,
    decimal PreviousStock,
    decimal NewStock,
    decimal AvailableStock)
{
    public static StockAdjustmentResult Missing => new(false, true, 0, 0, 0);

    public static StockAdjustmentResult Rejected(decimal available) => new(false, false, available, available, available);

    public static StockAdjustmentResult Ok(decimal previous, decimal updated) => new(true, false, previous, updated, updated);
}

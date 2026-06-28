namespace BusinessOS.Domain.Enums;

public enum StockTransactionType
{
    Purchase,
    Sale,
    Adjustment,
    Return,
    Damage,
    Transfer
}

public static class StockTransactionTypeNames
{
    public const string Purchase = nameof(StockTransactionType.Purchase);
    public const string Sale = nameof(StockTransactionType.Sale);
    public const string Adjustment = nameof(StockTransactionType.Adjustment);
    public const string Return = nameof(StockTransactionType.Return);
    public const string Damage = nameof(StockTransactionType.Damage);
    public const string Transfer = nameof(StockTransactionType.Transfer);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Purchase,
        Sale,
        Adjustment,
        Return,
        Damage,
        Transfer
    };

    public static bool IsValid(string? transactionType) =>
        transactionType is not null && All.Contains(transactionType);
}

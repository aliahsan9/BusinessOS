namespace BusinessOS.Application.Features.DataImports;

public static class DataImportEntityTypes
{
    public const string Products = "products";
    public const string Customers = "customers";
    public const string Suppliers = "suppliers";
    public const string Categories = "categories";
    public const string Sales = "sales";
    public const string Purchases = "purchases";
    public const string Expenses = "expenses";
    public const string CustomerPayments = "customer-payments";
    public const string SupplierPayments = "supplier-payments";
    public const string InventoryAdjustments = "inventory-adjustments";

    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        Products,
        Customers,
        Suppliers,
        Categories,
        Sales,
        Purchases,
        Expenses,
        CustomerPayments,
        SupplierPayments,
        InventoryAdjustments
    };

    public static IReadOnlyCollection<string> All => Supported;

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim().ToLowerInvariant();
        if (!Supported.Contains(trimmed))
            return false;

        normalized = trimmed;
        return true;
    }

    public static string NormalizeOrThrow(string value)
    {
        if (!TryNormalize(value, out var normalized))
        {
            throw new Common.Exceptions.BadRequestException(
                $"Unsupported entity type '{value}'. Supported: {string.Join(", ", Supported.OrderBy(x => x))}.");
        }

        return normalized;
    }

    public static bool IsQuotaCountedCreate(string entityType) =>
        entityType is Products or Customers;

    public static string? QuotaResourceName(string entityType) =>
        entityType switch
        {
            Products => "Products",
            Customers => "Customers",
            _ => null
        };
}

using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.DataImports.Models;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Features.DataImports.Services;

public static class ImportPreviewBuilder
{
    public static async Task<ImportPreviewResponse> BuildAsync(
        DataImportPreviewSession session,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var fields = ImportEntityDefinitions.GetFields(session.EntityType);
        var fieldDtos = fields.Select(f => new ImportFieldMappingDto(
            f.Key,
            f.Label,
            f.Required,
            session.ColumnMapping.TryGetValue(f.Key, out var col) ? col : null)).ToList();

        var existingKeys = await LoadExistingNaturalKeysAsync(session.EntityType, context, cancellationToken);
        var seenFileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previewRows = new List<ImportPreviewRowDto>();
        var createCount = 0;
        var updateCount = 0;
        var invalidRows = 0;

        for (var index = 0; index < session.RawRows.Count; index++)
        {
            var row = session.RawRows[index];
            var rowNumber = index + 2;
            var errors = ValidateRow(session.EntityType, row, session.ColumnMapping);
            var naturalKey = ResolveNaturalKey(session.EntityType, row, session.ColumnMapping);

            if (string.IsNullOrWhiteSpace(naturalKey))
                errors.Add("Natural key is missing.");

            if (!string.IsNullOrWhiteSpace(naturalKey) && !seenFileKeys.Add(naturalKey)
                && session.EntityType is not (
                    DataImportEntityTypes.Sales or DataImportEntityTypes.Purchases))
            {
                // Sales/purchases are denormalized line sheets — duplicate ImportKey+SKU is an error; same ImportKey across lines is expected.
                // For line sheets, uniqueness is ImportKey|SKU
                errors.Add($"Duplicate natural key '{naturalKey}' within this file.");
            }

            if (session.EntityType is DataImportEntityTypes.Sales or DataImportEntityTypes.Purchases)
            {
                // Override: allow same ImportKey; uniqueness is ImportKey|SKU already in ResolveNaturalKey for lines
            }

            string action = "Error";
            if (errors.Count == 0 && !string.IsNullOrWhiteSpace(naturalKey))
            {
                // For denormalized sales/purchases, create vs update is based on order/purchase import key
                var headerKey = ResolveHeaderKey(session.EntityType, row, session.ColumnMapping) ?? naturalKey;
                action = existingKeys.Contains(headerKey) || existingKeys.Contains(naturalKey)
                    ? "Update"
                    : "Create";
                if (action == "Create")
                    createCount++;
                else
                    updateCount++;
            }
            else
            {
                invalidRows++;
            }

            var values = BuildValuePreview(session.EntityType, row, session.ColumnMapping);
            previewRows.Add(new ImportPreviewRowDto(rowNumber, action, naturalKey, errors, values));
        }

        // Deduplicate create counts for denormalized line imports (count unique headers)
        if (session.EntityType is DataImportEntityTypes.Sales or DataImportEntityTypes.Purchases)
        {
            var uniqueCreates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var uniqueUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            createCount = 0;
            updateCount = 0;
            foreach (var row in previewRows.Where(r => r.Errors.Count == 0))
            {
                var header = ResolveHeaderKey(
                    session.EntityType,
                    session.RawRows[row.RowNumber - 2],
                    session.ColumnMapping) ?? row.NaturalKey;

                if (existingKeys.Contains(header))
                {
                    if (uniqueUpdates.Add(header))
                        updateCount++;
                }
                else if (uniqueCreates.Add(header))
                {
                    createCount++;
                }
            }
        }

        return new ImportPreviewResponse(
            session.PreviewId,
            session.EntityType,
            session.FileName,
            session.Format,
            session.ContentHash,
            session.CsvColumns,
            fieldDtos,
            session.ColumnMapping,
            session.RawRows.Count,
            session.RawRows.Count - invalidRows,
            invalidRows,
            createCount,
            updateCount,
            previewRows.Take(100).ToList(),
            null,
            session.DuplicateBatchDetected);
    }

    public static IReadOnlyList<ImportRowFailure> CollectFailures(DataImportPreviewSession session)
    {
        var failures = new List<ImportRowFailure>();
        var seenFileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < session.RawRows.Count; index++)
        {
            var row = session.RawRows[index];
            var rowNumber = index + 2;
            var errors = ValidateRow(session.EntityType, row, session.ColumnMapping);
            var naturalKey = ResolveNaturalKey(session.EntityType, row, session.ColumnMapping);

            if (string.IsNullOrWhiteSpace(naturalKey))
                errors.Add("Natural key is missing.");

            if (!string.IsNullOrWhiteSpace(naturalKey)
                && session.EntityType is not (DataImportEntityTypes.Sales or DataImportEntityTypes.Purchases)
                && !seenFileKeys.Add(naturalKey))
            {
                errors.Add($"Duplicate natural key '{naturalKey}' within this file.");
            }
            else if (!string.IsNullOrWhiteSpace(naturalKey)
                     && session.EntityType is DataImportEntityTypes.Sales or DataImportEntityTypes.Purchases)
            {
                seenFileKeys.Add(naturalKey); // line uniqueness
            }

            if (errors.Count == 0)
                continue;

            failures.Add(new ImportRowFailure(
                rowNumber,
                naturalKey,
                errors,
                BuildValuePreview(session.EntityType, row, session.ColumnMapping)));
        }

        return failures;
    }

    private static IReadOnlyDictionary<string, string> BuildValuePreview(
        string entityType,
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping)
    {
        var fields = ImportEntityDefinitions.GetFields(entityType);
        var result = new Dictionary<string, string>();
        foreach (var field in fields)
            result[field.Key] = ImportColumnMapper.GetMappedValue(row, mapping, field.Key);
        return result;
    }

    public static List<string> ValidateRow(
        string entityType,
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping)
    {
        var errors = new List<string>();
        foreach (var field in ImportEntityDefinitions.GetFields(entityType))
        {
            var value = ImportColumnMapper.GetMappedValue(row, mapping, field.Key);
            if (field.Required && string.IsNullOrWhiteSpace(value))
                errors.Add($"{field.Label} is required.");
        }

        if (entityType == DataImportEntityTypes.Products)
        {
            ValidateDecimal(row, mapping, "CostPrice", "Cost Price", errors);
            ValidateDecimal(row, mapping, "SalePrice", "Sale Price", errors);
            ValidateDecimal(row, mapping, "CurrentStock", "Current Stock", errors);
        }

        if (entityType is DataImportEntityTypes.Customers or DataImportEntityTypes.Suppliers)
        {
            var email = ImportColumnMapper.GetMappedValue(row, mapping, "Email");
            if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
                errors.Add("Email format is invalid.");
        }

        if (entityType is DataImportEntityTypes.Sales or DataImportEntityTypes.Purchases
            or DataImportEntityTypes.CustomerPayments or DataImportEntityTypes.SupplierPayments
            or DataImportEntityTypes.Expenses or DataImportEntityTypes.InventoryAdjustments)
        {
            ValidateDecimal(row, mapping, "Quantity", "Quantity", errors);
            ValidateDecimal(row, mapping, "UnitPrice", "Unit Price", errors);
            ValidateDecimal(row, mapping, "Amount", "Amount", errors);
            ValidateDate(row, mapping, "OrderDate", "Order Date", errors);
            ValidateDate(row, mapping, "PurchaseDate", "Purchase Date", errors);
            ValidateDate(row, mapping, "ExpenseDate", "Expense Date", errors);
            ValidateDate(row, mapping, "PaymentDate", "Payment Date", errors);
        }

        if (entityType == DataImportEntityTypes.Sales)
        {
            var email = ImportColumnMapper.GetMappedValue(row, mapping, "CustomerEmail");
            if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
                errors.Add("Customer Email format is invalid.");
        }

        return errors;
    }

    private static void ValidateDecimal(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string key,
        string label,
        List<string> errors)
    {
        var value = ImportColumnMapper.GetMappedValue(row, mapping, key);
        if (!string.IsNullOrWhiteSpace(value) && !decimal.TryParse(value, out _))
            errors.Add($"{label} must be a number.");
    }

    private static void ValidateDate(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string key,
        string label,
        List<string> errors)
    {
        var value = ImportColumnMapper.GetMappedValue(row, mapping, key);
        if (!string.IsNullOrWhiteSpace(value) && !DateTime.TryParse(value, out _))
            errors.Add($"{label} must be a valid date (YYYY-MM-DD).");
    }

    public static string ResolveNaturalKey(
        string entityType,
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping)
    {
        return entityType switch
        {
            DataImportEntityTypes.Products =>
                ImportColumnMapper.GetMappedValue(row, mapping, "SKU"),
            DataImportEntityTypes.Customers =>
                NormalizeEmail(ImportColumnMapper.GetMappedValue(row, mapping, "Email")),
            DataImportEntityTypes.Suppliers =>
                ResolveSupplierKey(row, mapping),
            DataImportEntityTypes.Categories =>
                NormalizeName(ImportColumnMapper.GetMappedValue(row, mapping, "Name")),
            DataImportEntityTypes.Sales =>
                $"{ImportColumnMapper.GetMappedValue(row, mapping, "ImportKey").Trim()}|{ImportColumnMapper.GetMappedValue(row, mapping, "ProductSKU").Trim()}",
            DataImportEntityTypes.Purchases =>
                $"{ImportColumnMapper.GetMappedValue(row, mapping, "ImportKey").Trim()}|{ImportColumnMapper.GetMappedValue(row, mapping, "ProductSKU").Trim()}",
            DataImportEntityTypes.Expenses =>
                ImportColumnMapper.GetMappedValue(row, mapping, "ImportKey").Trim(),
            DataImportEntityTypes.CustomerPayments =>
                ImportColumnMapper.GetMappedValue(row, mapping, "ImportKey").Trim(),
            DataImportEntityTypes.SupplierPayments =>
                ImportColumnMapper.GetMappedValue(row, mapping, "ImportKey").Trim(),
            DataImportEntityTypes.InventoryAdjustments =>
                ImportColumnMapper.GetMappedValue(row, mapping, "ImportKey").Trim(),
            _ => string.Empty
        };
    }

    public static string? ResolveHeaderKey(
        string entityType,
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping) =>
        entityType switch
        {
            DataImportEntityTypes.Sales or DataImportEntityTypes.Purchases =>
                ImportColumnMapper.GetMappedValue(row, mapping, "ImportKey").Trim(),
            _ => null
        };

    public static string ResolveSupplierKey(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping)
    {
        var email = NormalizeEmail(ImportColumnMapper.GetMappedValue(row, mapping, "Email"));
        if (!string.IsNullOrWhiteSpace(email))
            return $"email:{email}";

        var name = NormalizeName(ImportColumnMapper.GetMappedValue(row, mapping, "Name"));
        return string.IsNullOrWhiteSpace(name) ? string.Empty : $"name:{name}";
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    private static async Task<HashSet<string>> LoadExistingNaturalKeysAsync(
        string entityType,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        return entityType switch
        {
            DataImportEntityTypes.Products => (await context.Products
                .Select(x => x.SKU)
                .ToListAsync(cancellationToken))
                .Select(s => s.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            DataImportEntityTypes.Customers => (await context.Customers
                .Select(x => x.Email)
                .ToListAsync(cancellationToken))
                .Select(NormalizeEmail)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            DataImportEntityTypes.Suppliers => await LoadSupplierKeysAsync(context, cancellationToken),
            DataImportEntityTypes.Categories => (await context.Categories
                .Select(x => x.Name)
                .ToListAsync(cancellationToken))
                .Select(NormalizeName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            DataImportEntityTypes.Sales => (await context.Orders
                .Where(x => x.ExternalImportKey != null)
                .Select(x => x.ExternalImportKey!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            DataImportEntityTypes.Purchases => (await context.Purchases
                .Where(x => x.IdempotencyKey != null)
                .Select(x => x.IdempotencyKey!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            DataImportEntityTypes.Expenses => (await context.Expenses
                .Where(x => x.ExternalImportKey != null)
                .Select(x => x.ExternalImportKey!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            DataImportEntityTypes.CustomerPayments => (await context.Payments
                .Where(x => x.ExternalImportKey != null)
                .Select(x => x.ExternalImportKey!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            DataImportEntityTypes.SupplierPayments => (await context.PurchaseInvoices
                .Where(x => x.ExternalImportKey != null)
                .Select(x => x.ExternalImportKey!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            DataImportEntityTypes.InventoryAdjustments => (await context.StockTransactions
                .Where(x => x.ReferenceNumber != null && x.ReferenceNumber.StartsWith("import:"))
                .Select(x => x.ReferenceNumber!)
                .ToListAsync(cancellationToken))
                .Select(r => r.StartsWith("import:", StringComparison.OrdinalIgnoreCase) ? r["import:".Length..] : r)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            _ => []
        };
    }

    private static async Task<HashSet<string>> LoadSupplierKeysAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var suppliers = await context.Suppliers
            .Select(x => new { x.Email, x.Name })
            .ToListAsync(cancellationToken);

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var supplier in suppliers)
        {
            if (!string.IsNullOrWhiteSpace(supplier.Email))
                keys.Add($"email:{NormalizeEmail(supplier.Email)}");
            else if (!string.IsNullOrWhiteSpace(supplier.Name))
                keys.Add($"name:{NormalizeName(supplier.Name)}");
        }

        return keys;
    }
}

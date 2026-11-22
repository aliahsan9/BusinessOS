namespace BusinessOS.Application.Features.DataImports.Services;

public sealed record ImportFieldDefinition(
    string Key,
    string Label,
    bool Required,
    params string[] Aliases);

public static class ImportEntityDefinitions
{
    public static IReadOnlyList<ImportFieldDefinition> GetFields(string entityType) =>
        entityType switch
        {
            DataImportEntityTypes.Categories => CategoryFields,
            DataImportEntityTypes.Products => ProductFields,
            DataImportEntityTypes.Customers => CustomerFields,
            DataImportEntityTypes.Suppliers => SupplierFields,
            DataImportEntityTypes.Sales => SalesFields,
            DataImportEntityTypes.Purchases => PurchaseFields,
            DataImportEntityTypes.Expenses => ExpenseFields,
            DataImportEntityTypes.CustomerPayments => CustomerPaymentFields,
            DataImportEntityTypes.SupplierPayments => SupplierPaymentFields,
            DataImportEntityTypes.InventoryAdjustments => InventoryAdjustmentFields,
            _ => throw new Common.Exceptions.BadRequestException($"Unknown entity type '{entityType}'.")
        };

    public static string BuildTemplateCsv(string entityType)
    {
        var fields = GetFields(entityType);
        var header = string.Join(',', fields.Select(f => QuoteCsv(f.Label)));
        var sample = entityType switch
        {
            DataImportEntityTypes.Categories =>
                "Electronics,Mobile phones and accessories",
            DataImportEntityTypes.Products =>
                "SKU-SAM-A15,Samsung Galaxy A15,Electronics,6.5 inch display,48500,59999,5,12,true,8801234567890",
            DataImportEntityTypes.Customers =>
                "ahmed.khan@email.pk,Ahmed,Khan,+92-300-1234567,Shop 12 Tariq Road,Karachi,Pakistan,75500,Khan Traders,",
            DataImportEntityTypes.Suppliers =>
                "Lahore Distributors,orders@lahoredistributors.pk,+92-42-111222333,Main Boulevard Gulberg,Ali Raza,",
            DataImportEntityTypes.Sales =>
                "SALE-2024-001,INV-1001,2024-06-15,ahmed.khan@email.pk,SKU-SAM-A15,1,59999,0,0,Paid",
            DataImportEntityTypes.Purchases =>
                "PO-2024-001,PO-1001,2024-05-01,orders@lahoredistributors.pk,SKU-SAM-A15,10,48500,Received",
            DataImportEntityTypes.Expenses =>
                "EXP-2024-001,Shop Rent June,Rent,85000,2024-06-01,Bank Transfer,Property Manager,RENT-JUN,",
            DataImportEntityTypes.CustomerPayments =>
                "PAY-2024-001,SALE-2024-001,59999,2024-06-16,JazzCash,JC-998877",
            DataImportEntityTypes.SupplierPayments =>
                "SPAY-2024-001,PO-2024-001,485000,2024-05-10,Bank Transfer,TRX-445566",
            DataImportEntityTypes.InventoryAdjustments =>
                "ADJ-2024-001,SKU-SAM-A15,2,Adjustment,2024-06-20,Opening stock correction",
            _ => string.Empty
        };

        return $"{header}\r\n{sample}\r\n";
    }

    private static string QuoteCsv(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static readonly ImportFieldDefinition[] CategoryFields =
    [
        new("Name", "Name", true, "name", "category", "category name", "title"),
        new("Description", "Description", false, "description", "desc", "details")
    ];

    private static readonly ImportFieldDefinition[] ProductFields =
    [
        new("SKU", "SKU", true, "sku", "product sku", "code", "item code"),
        new("Name", "Name", true, "name", "product name", "title"),
        new("CategoryName", "Category", true, "category", "category name", "product category"),
        new("Description", "Description", false, "description", "desc"),
        new("CostPrice", "Cost Price", false, "cost", "cost price", "purchase price", "cost pkr", "cost_pkr", "costpkr"),
        new("SalePrice", "Sale Price", false, "price", "sale price", "selling price", "sell price", "sell_price_pkr", "sellpricepkr", "unit price"),
        new("ReorderLevel", "Reorder Level", false, "reorder", "reorder level", "min stock", "reorder_level"),
        new("CurrentStock", "Current Stock", false, "stock", "quantity", "qty", "on hand", "stock qty", "stock_qty", "stockqty", "opening stock"),
        new("IsActive", "Is Active", false, "active", "is active", "enabled"),
        new("Barcode", "Barcode", false, "barcode", "upc", "ean")
    ];

    private static readonly ImportFieldDefinition[] CustomerFields =
    [
        new("Email", "Email", true, "email", "e-mail", "email address"),
        new("FirstName", "First Name", true, "first name", "firstname", "given name"),
        new("LastName", "Last Name", true, "last name", "lastname", "surname", "family name"),
        new("PhoneNumber", "Phone", true, "phone", "phone number", "mobile", "tel"),
        new("Address", "Address", false, "address", "street", "street address"),
        new("City", "City", false, "city", "town"),
        new("Country", "Country", false, "country", "nation"),
        new("PostalCode", "Postal Code", false, "postal code", "zip", "zip code", "postcode"),
        new("Company", "Company", false, "company", "organization", "org"),
        new("Notes", "Notes", false, "notes", "comments"),
        new("IsActive", "Is Active", false, "active", "is active")
    ];

    private static readonly ImportFieldDefinition[] SupplierFields =
    [
        new("Name", "Name", true, "name", "supplier", "supplier name", "vendor"),
        new("Email", "Email", true, "email", "e-mail"),
        new("Phone", "Phone", true, "phone", "phone number", "tel"),
        new("Address", "Address", false, "address", "street"),
        new("ContactPerson", "Contact Person", false, "contact", "contact person", "buyer"),
        new("Notes", "Notes", false, "notes", "comments")
    ];

    private static readonly ImportFieldDefinition[] SalesFields =
    [
        new("ImportKey", "Import Key", true, "import key", "external key", "idempotency key", "sale id", "sale key"),
        new("OrderNumber", "Order Number", true, "order number", "invoice number", "invoice no", "sale number", "bill no"),
        new("OrderDate", "Order Date", true, "order date", "sale date", "invoice date", "date"),
        new("CustomerEmail", "Customer Email", true, "customer email", "email", "customer"),
        new("ProductSKU", "Product SKU", true, "sku", "product sku", "item code", "product code"),
        new("Quantity", "Quantity", true, "qty", "quantity", "units"),
        new("UnitPrice", "Unit Price", true, "unit price", "price", "sale price", "rate"),
        new("Discount", "Discount", false, "discount", "order discount"),
        new("Tax", "Tax", false, "tax", "gst", "sales tax"),
        new("PaymentStatus", "Payment Status", false, "payment status", "paid status")
    ];

    private static readonly ImportFieldDefinition[] PurchaseFields =
    [
        new("ImportKey", "Import Key", true, "import key", "external key", "idempotency key", "purchase id"),
        new("ReferenceNumber", "Reference Number", true, "reference", "po number", "purchase number", "bill no"),
        new("PurchaseDate", "Purchase Date", true, "purchase date", "date", "order date"),
        new("SupplierEmail", "Supplier Email", true, "supplier email", "email", "vendor email"),
        new("ProductSKU", "Product SKU", true, "sku", "product sku", "item code"),
        new("Quantity", "Quantity", true, "qty", "quantity", "units"),
        new("UnitPrice", "Unit Price", true, "unit price", "cost", "cost price", "rate"),
        new("Status", "Status", false, "status", "po status")
    ];

    private static readonly ImportFieldDefinition[] ExpenseFields =
    [
        new("ImportKey", "Import Key", true, "import key", "external key", "expense id"),
        new("Title", "Title", true, "title", "expense", "description", "name"),
        new("CategoryName", "Category", true, "category", "expense category"),
        new("Amount", "Amount", true, "amount", "total", "cost"),
        new("ExpenseDate", "Expense Date", true, "expense date", "date"),
        new("PaymentMethod", "Payment Method", false, "payment method", "method", "paid via"),
        new("Vendor", "Vendor", false, "vendor", "payee", "supplier"),
        new("ReferenceNumber", "Reference Number", false, "reference", "reference number", "ref"),
        new("Description", "Description", false, "notes", "details", "description")
    ];

    private static readonly ImportFieldDefinition[] CustomerPaymentFields =
    [
        new("ImportKey", "Import Key", true, "import key", "external key", "payment id"),
        new("OrderImportKey", "Order Import Key", true, "order import key", "sale import key", "order key", "sale key"),
        new("Amount", "Amount", true, "amount", "paid", "payment amount"),
        new("PaymentDate", "Payment Date", true, "payment date", "date"),
        new("PaymentMethod", "Payment Method", true, "payment method", "method", "paid via"),
        new("ReferenceNo", "Reference", false, "reference", "reference no", "txn id", "transaction id")
    ];

    private static readonly ImportFieldDefinition[] SupplierPaymentFields =
    [
        new("ImportKey", "Import Key", true, "import key", "external key", "payment id"),
        new("PurchaseImportKey", "Purchase Import Key", true, "purchase import key", "po import key", "purchase key", "po key"),
        new("Amount", "Amount", true, "amount", "paid", "payment amount"),
        new("PaymentDate", "Payment Date", true, "payment date", "date"),
        new("PaymentMethod", "Payment Method", false, "payment method", "method"),
        new("ReferenceNo", "Reference", false, "reference", "reference no", "txn id")
    ];

    private static readonly ImportFieldDefinition[] InventoryAdjustmentFields =
    [
        new("ImportKey", "Import Key", true, "import key", "external key", "adjustment id"),
        new("ProductSKU", "Product SKU", true, "sku", "product sku", "item code"),
        new("Quantity", "Quantity", true, "qty", "quantity", "adjustment qty"),
        new("TransactionType", "Transaction Type", true, "type", "transaction type", "adjustment type"),
        new("AdjustmentDate", "Adjustment Date", false, "date", "adjustment date"),
        new("Notes", "Notes", false, "notes", "reason", "comments")
    ];
}

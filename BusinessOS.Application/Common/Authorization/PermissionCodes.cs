namespace BusinessOS.Application.Common.Authorization;

public static class PermissionCodes
{
    public const string CategoryCreate = "Category.Create";
    public const string CategoryView = "Category.View";
    public const string CategoryUpdate = "Category.Update";
    public const string CategoryDelete = "Category.Delete";

    public const string ProductCreate = "Product.Create";
    public const string ProductView = "Product.View";
    public const string ProductUpdate = "Product.Update";
    public const string ProductDelete = "Product.Delete";

    public const string CustomerCreate = "Customer.Create";
    public const string CustomerView = "Customer.View";
    public const string CustomerUpdate = "Customer.Update";
    public const string CustomerDelete = "Customer.Delete";

    public const string OrderCreate = "Order.Create";
    public const string OrderView = "Order.View";
    public const string OrderUpdate = "Order.Update";
    public const string OrderDelete = "Order.Delete";

    public const string SupplierCreate = "Supplier.Create";
    public const string SupplierView = "Supplier.View";
    public const string SupplierUpdate = "Supplier.Update";
    public const string SupplierDelete = "Supplier.Delete";

    public const string PurchaseOrderCreate = "PurchaseOrder.Create";
    public const string PurchaseOrderView = "PurchaseOrder.View";
    public const string PurchaseOrderUpdate = "PurchaseOrder.Update";
    public const string PurchaseOrderDelete = "PurchaseOrder.Delete";

    public const string PaymentCreate = "Payment.Create";
    public const string PaymentView = "Payment.View";
    public const string PaymentUpdate = "Payment.Update";
    public const string PaymentDelete = "Payment.Delete";

    public const string InvoiceCreate = "Invoice.Create";
    public const string InvoiceView = "Invoice.View";
    public const string InvoiceUpdate = "Invoice.Update";
    public const string InvoiceDelete = "Invoice.Delete";

    public const string QuotationCreate = "Quotation.Create";
    public const string QuotationView = "Quotation.View";
    public const string QuotationUpdate = "Quotation.Update";
    public const string QuotationDelete = "Quotation.Delete";

    public const string InventoryView = "Inventory.View";
    public const string InventoryUpdate = "Inventory.Update";
    public const string InventoryAdjust = "Inventory.Adjust";

    public const string UserCreate = "User.Create";
    public const string UserView = "User.View";
    public const string UserUpdate = "User.Update";
    public const string UserDelete = "User.Delete";

    public const string RoleCreate = "Role.Create";
    public const string RoleView = "Role.View";
    public const string RoleUpdate = "Role.Update";
    public const string RoleDelete = "Role.Delete";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new("Create Category", CategoryCreate, "Create product categories", "Category"),
        new("View Categories", CategoryView, "View product categories", "Category"),
        new("Update Category", CategoryUpdate, "Update product categories", "Category"),
        new("Delete Category", CategoryDelete, "Delete product categories", "Category"),

        new("Create Product", ProductCreate, "Create products", "Product"),
        new("View Products", ProductView, "View products", "Product"),
        new("Update Product", ProductUpdate, "Update products", "Product"),
        new("Delete Product", ProductDelete, "Delete products", "Product"),

        new("Create Customer", CustomerCreate, "Create customers", "Customer"),
        new("View Customers", CustomerView, "View customers", "Customer"),
        new("Update Customer", CustomerUpdate, "Update customers", "Customer"),
        new("Delete Customer", CustomerDelete, "Delete customers", "Customer"),

        new("Create Order", OrderCreate, "Create orders", "Order"),
        new("View Orders", OrderView, "View orders", "Order"),
        new("Update Order", OrderUpdate, "Update orders", "Order"),
        new("Delete Order", OrderDelete, "Delete orders", "Order"),

        new("Create Supplier", SupplierCreate, "Create suppliers", "Supplier"),
        new("View Suppliers", SupplierView, "View suppliers", "Supplier"),
        new("Update Supplier", SupplierUpdate, "Update suppliers", "Supplier"),
        new("Delete Supplier", SupplierDelete, "Delete suppliers", "Supplier"),

        new("Create Purchase Order", PurchaseOrderCreate, "Create purchase orders", "PurchaseOrder"),
        new("View Purchase Orders", PurchaseOrderView, "View purchase orders", "PurchaseOrder"),
        new("Update Purchase Order", PurchaseOrderUpdate, "Update purchase orders", "PurchaseOrder"),
        new("Delete Purchase Order", PurchaseOrderDelete, "Delete purchase orders", "PurchaseOrder"),

        new("Create Payment", PaymentCreate, "Create payments", "Payment"),
        new("View Payments", PaymentView, "View payments", "Payment"),
        new("Update Payment", PaymentUpdate, "Update payments", "Payment"),
        new("Delete Payment", PaymentDelete, "Delete payments", "Payment"),

        new("Create Invoice", InvoiceCreate, "Create invoices", "Invoice"),
        new("View Invoices", InvoiceView, "View invoices", "Invoice"),
        new("Update Invoice", InvoiceUpdate, "Update invoices", "Invoice"),
        new("Delete Invoice", InvoiceDelete, "Delete invoices", "Invoice"),

        new("Create Quotation", QuotationCreate, "Create quotations", "Quotation"),
        new("View Quotations", QuotationView, "View quotations", "Quotation"),
        new("Update Quotation", QuotationUpdate, "Update quotations", "Quotation"),
        new("Delete Quotation", QuotationDelete, "Delete quotations", "Quotation"),

        new("View Inventory", InventoryView, "View inventory records", "Inventory"),
        new("Update Inventory", InventoryUpdate, "Update inventory thresholds", "Inventory"),
        new("Adjust Inventory", InventoryAdjust, "Adjust stock levels", "Inventory"),

        new("Create User", UserCreate, "Create users", "User"),
        new("View Users", UserView, "View users", "User"),
        new("Update User", UserUpdate, "Update users", "User"),
        new("Delete User", UserDelete, "Delete users", "User"),

        new("Create Role", RoleCreate, "Create roles", "Role"),
        new("View Roles", RoleView, "View roles and permissions", "Role"),
        new("Update Role", RoleUpdate, "Update roles", "Role"),
        new("Delete Role", RoleDelete, "Delete roles", "Role")
    ];

    public static readonly IReadOnlySet<string> ViewOnly = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CategoryView,
        ProductView,
        CustomerView,
        OrderView,
        SupplierView,
        PurchaseOrderView,
        PaymentView,
        InvoiceView,
        QuotationView,
        InventoryView,
        UserView,
        RoleView
    };

    public static readonly IReadOnlySet<string> ManagerPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ProductCreate, ProductView, ProductUpdate, ProductDelete,
        CustomerCreate, CustomerView, CustomerUpdate, CustomerDelete,
        OrderCreate, OrderView, OrderUpdate, OrderDelete,
        SupplierCreate, SupplierView, SupplierUpdate, SupplierDelete,
        PurchaseOrderCreate, PurchaseOrderView, PurchaseOrderUpdate, PurchaseOrderDelete,
        PaymentCreate, PaymentView, PaymentUpdate, PaymentDelete,
        InvoiceCreate, InvoiceView, InvoiceUpdate, InvoiceDelete,
        QuotationCreate, QuotationView, QuotationUpdate, QuotationDelete,
        InventoryView, InventoryUpdate, InventoryAdjust
    };

    public static readonly IReadOnlySet<string> SalesPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CustomerCreate, CustomerView, CustomerUpdate, CustomerDelete,
        OrderCreate, OrderView, OrderUpdate, OrderDelete,
        PaymentCreate, PaymentView, PaymentUpdate, PaymentDelete,
        InvoiceCreate, InvoiceView, InvoiceUpdate, InvoiceDelete,
        QuotationCreate, QuotationView, QuotationUpdate, QuotationDelete
    };

    public static readonly IReadOnlySet<string> InventoryManagerPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        InventoryView, InventoryUpdate, InventoryAdjust,
        ProductCreate, ProductView, ProductUpdate, ProductDelete,
        SupplierCreate, SupplierView, SupplierUpdate, SupplierDelete,
        PurchaseOrderCreate, PurchaseOrderView, PurchaseOrderUpdate, PurchaseOrderDelete
    };
}

public sealed record PermissionDefinition(
    string Name,
    string Code,
    string Description,
    string Category);

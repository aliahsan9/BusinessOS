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

    public const string ExpenseCreate = "Expense.Create";
    public const string ExpenseView = "Expense.View";
    public const string ExpenseUpdate = "Expense.Update";
    public const string ExpenseDelete = "Expense.Delete";

    public const string ExpenseCategoryCreate = "ExpenseCategory.Create";
    public const string ExpenseCategoryView = "ExpenseCategory.View";
    public const string ExpenseCategoryUpdate = "ExpenseCategory.Update";
    public const string ExpenseCategoryDelete = "ExpenseCategory.Delete";

    public const string FinanceView = "Finance.View";

    public const string AuditView = "Audit.View";

    public const string NotificationView = "Notification.View";
    public const string NotificationUpdate = "Notification.Update";

    public const string ActivityView = "Activity.View";

    public const string ReportView = "Report.View";

    public const string SettingsView = "Settings.View";
    public const string SettingsUpdate = "Settings.Update";
    public const string SettingsManage = "Settings.Manage";

    public const string SystemAdminView = "SystemAdmin.View";
    public const string SystemAdminManage = "SystemAdmin.Manage";

    public const string ProjectCreate = "Projects.Create";
    public const string ProjectView = "Projects.View";
    public const string ProjectUpdate = "Projects.Update";
    public const string ProjectDelete = "Projects.Delete";

    public const string TaskCreate = "Tasks.Create";
    public const string TaskView = "Tasks.View";
    public const string TaskUpdate = "Tasks.Update";
    public const string TaskDelete = "Tasks.Delete";

    public const string AnalyticsView = "Analytics.View";

    public const string TeamView = "Team.View";
    public const string TeamManage = "Team.Manage";
    public const string TeamInvite = "Team.Invite";

    public const string OrganizationView = "Organization.View";
    public const string OrganizationManage = "Organization.Manage";

    public const string TenantView = "Tenant.View";
    public const string TenantManage = "Tenant.Manage";

    public const string SubscriptionView = "Subscription.View";
    public const string SubscriptionManage = "Subscription.Manage";

    public const string UserManage = "Users.Manage";

    public const string EmployeeView = "Employee.View";
    public const string EmployeeManage = "Employee.Manage";
    public const string EmployeeSalary = "Employee.Salary";
    public const string AttendanceManage = "Attendance.Manage";
    public const string LeaveManage = "Leave.Manage";
    public const string AnnouncementView = "Announcement.View";
    public const string AnnouncementManage = "Announcement.Manage";
    public const string PerformanceView = "Performance.View";
    public const string PerformanceManage = "Performance.Manage";
    public const string LeadView = "Lead.View";
    public const string LeadManage = "Lead.Manage";
    public const string DealView = "Deal.View";
    public const string DealManage = "Deal.Manage";
    public const string WarehouseView = "Warehouse.View";
    public const string WarehouseManage = "Warehouse.Manage";
    public const string FulfillmentManage = "Fulfillment.Manage";
    public const string ReturnManage = "Return.Manage";
    public const string TimeView = "Time.View";
    public const string TimeManage = "Time.Manage";
    public const string PortalManage = "Portal.Manage";
    public const string StorefrontView = "Storefront.View";
    public const string StorefrontManage = "Storefront.Manage";

    public const string AgentExecute = "Agent.Execute";
    public const string NalvaView = "Nalva.View";
    public const string NalvaManage = "Nalva.Manage";
    public const string NalvaApprove = "Nalva.Approve";

    public const string EnterpriseView = "Enterprise.View";
    public const string EnterpriseManage = "Enterprise.Manage";
    public const string OrgBranchView = "Org.Branch.View";
    public const string OrgBranchManage = "Org.Branch.Manage";
    public const string ApprovalPolicyView = "Approval.Policy.View";
    public const string ApprovalPolicyManage = "Approval.Policy.Manage";
    public const string ApprovalDecide = "Approval.Decide";
    public const string AuditExport = "Audit.Export";
    public const string SsoManage = "Sso.Manage";
    public const string AgentGovernanceManage = "Agent.Governance.Manage";

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
        new("Delete Role", RoleDelete, "Delete roles", "Role"),

        new("Create Expense", ExpenseCreate, "Create expenses", "Expense"),
        new("View Expenses", ExpenseView, "View expenses", "Expense"),
        new("Update Expense", ExpenseUpdate, "Update expenses", "Expense"),
        new("Delete Expense", ExpenseDelete, "Delete expenses", "Expense"),

        new("Create Expense Category", ExpenseCategoryCreate, "Create expense categories", "ExpenseCategory"),
        new("View Expense Categories", ExpenseCategoryView, "View expense categories", "ExpenseCategory"),
        new("Update Expense Category", ExpenseCategoryUpdate, "Update expense categories", "ExpenseCategory"),
        new("Delete Expense Category", ExpenseCategoryDelete, "Delete expense categories", "ExpenseCategory"),

        new("View Finance", FinanceView, "View financial dashboard and P&L", "Finance"),
        new("View Audit Logs", AuditView, "View system audit logs", "Audit"),
        new("View Notifications", NotificationView, "View notifications", "Notification"),
        new("Update Notifications", NotificationUpdate, "Manage notification preferences", "Notification"),
        new("View Activity", ActivityView, "View activity timeline", "Activity"),
        new("View Reports", ReportView, "View and export reports", "Report"),
        new("View Settings", SettingsView, "View business settings", "Settings"),
        new("Update Settings", SettingsUpdate, "Update business settings", "Settings"),
        new("Manage Settings", SettingsManage, "Full settings management", "Settings"),
        new("View System Admin", SystemAdminView, "View system administration for the current tenant", "SystemAdmin"),
        new("Manage Platform", SystemAdminManage, "Platform-only billing and contract administration", "SystemAdmin"),

        new("Create Project", ProjectCreate, "Create projects", "Project"),
        new("View Projects", ProjectView, "View projects", "Project"),
        new("Update Project", ProjectUpdate, "Update projects", "Project"),
        new("Delete Project", ProjectDelete, "Delete projects", "Project"),

        new("Create Task", TaskCreate, "Create tasks", "Task"),
        new("View Tasks", TaskView, "View tasks", "Task"),
        new("Update Task", TaskUpdate, "Update tasks", "Task"),
        new("Delete Task", TaskDelete, "Delete tasks", "Task"),

        new("View Analytics", AnalyticsView, "View business analytics", "Analytics"),

        new("View Team", TeamView, "View team members and dashboard", "Team"),
        new("Manage Team", TeamManage, "Manage team members and roles", "Team"),
        new("Invite Team Members", TeamInvite, "Invite new team members", "Team"),

        new("View Organization", OrganizationView, "View organization profile", "Organization"),
        new("Manage Organization", OrganizationManage, "Manage organization settings", "Organization"),

        new("View Tenant", TenantView, "View tenant profile and dashboard", "Tenant"),
        new("Manage Tenant", TenantManage, "Manage tenant settings and branding", "Tenant"),

        new("View Subscription", SubscriptionView, "View subscription details", "Subscription"),
        new("Manage Subscription", SubscriptionManage, "Manage subscription billing", "Subscription"),

        new("Manage Users", UserManage, "Full user management access", "User"),
        new("Execute AI Agent", AgentExecute, "Run explicitly authorized AI agent operations", "Agent"),
        new("View Nalva Workforce", NalvaView, "View the Nalva.ai workforce, agents, and execution history", "Nalva"),
        new("Manage Nalva Workforce", NalvaManage, "Activate, configure, and assign Nalva.ai agents and workflows", "Nalva"),
        new("Approve Nalva Actions", NalvaApprove, "Approve or reject restricted actions requested by Nalva.ai agents", "Nalva"),

        new("View Employees", EmployeeView, "View employee directory", "Employee"),
        new("Manage Employees", EmployeeManage, "Manage employees, departments, and teams", "Employee"),
        new("View Salaries", EmployeeSalary, "View and update employee salaries", "Employee"),
        new("Manage Attendance", AttendanceManage, "Correct attendance and view company attendance", "Employee"),
        new("Manage Leave", LeaveManage, "Approve or reject leave requests", "Employee"),
        new("View Announcements", AnnouncementView, "Read company announcements", "Employee"),
        new("Manage Announcements", AnnouncementManage, "Publish announcements", "Employee"),
        new("View Performance", PerformanceView, "View own performance records", "Employee"),
        new("Manage Performance", PerformanceManage, "Manage goals and reviews", "Employee"),
        new("View Leads", LeadView, "View leads and pipeline", "CRM"),
        new("Manage Leads", LeadManage, "Create and convert leads", "CRM"),
        new("View Deals", DealView, "View deals", "CRM"),
        new("Manage Deals", DealManage, "Manage deals and follow-ups", "CRM"),
        new("View Warehouses", WarehouseView, "View warehouses and stock", "Inventory"),
        new("Manage Warehouses", WarehouseManage, "Manage warehouses and transfers", "Inventory"),
        new("Manage Fulfillment", FulfillmentManage, "Process order fulfillment", "Order"),
        new("Manage Returns", ReturnManage, "Approve returns and restocking", "Order"),
        new("View Time", TimeView, "View own time entries", "Project"),
        new("Manage Time", TimeManage, "Approve timesheets", "Project"),
        new("Manage Client Portal", PortalManage, "Invite clients to project portal", "Project"),
        new("View Storefront", StorefrontView, "View the company storefront and public profile", "Storefront"),
        new("Manage Storefront", StorefrontManage, "Publish the company profile, catalog, and reviews", "Storefront"),

        new("View Enterprise", EnterpriseView, "View enterprise administration surfaces", "Enterprise"),
        new("Manage Enterprise", EnterpriseManage, "Manage enterprise hierarchy, security, and governance", "Enterprise"),
        new("View Branches", OrgBranchView, "View company branches", "Organization"),
        new("Manage Branches", OrgBranchManage, "Create and update company branches", "Organization"),
        new("View Approval Policies", ApprovalPolicyView, "View approval policies and requests", "Approval"),
        new("Manage Approval Policies", ApprovalPolicyManage, "Configure multi-level approval policies", "Approval"),
        new("Decide Approvals", ApprovalDecide, "Approve or reject pending approval requests", "Approval"),
        new("Export Audit Logs", AuditExport, "Export enterprise audit logs", "Audit"),
        new("Manage SSO", SsoManage, "Configure enterprise SSO and MFA policies", "Security"),
        new("Manage Agent Governance", AgentGovernanceManage, "Configure enterprise AI agent restrictions", "Nalva")
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
        RoleView,
        ExpenseView,
        ExpenseCategoryView,
        FinanceView,
        AuditView,
        NotificationView,
        ReportView,
        SettingsView,
        ActivityView,
        ProjectView,
        TaskView,
        AnalyticsView,
        TeamView,
        OrganizationView,
        TenantView,
        SubscriptionView,
        EmployeeView,
        AnnouncementView,
        PerformanceView,
        LeadView,
        DealView,
        WarehouseView,
        TimeView,
        StorefrontView,
        EnterpriseView,
        OrgBranchView,
        ApprovalPolicyView,
        NalvaView
    };

    public static readonly IReadOnlySet<string> EmployeePermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ProjectView,
        TaskView,
        TaskUpdate,
        ActivityView,
        NotificationView,
        EmployeeView,
        AnnouncementView,
        PerformanceView,
        TimeView
    };

    public static readonly IReadOnlySet<string> ManagerPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CustomerCreate, CustomerView, CustomerUpdate, CustomerDelete,
        ProjectCreate, ProjectView, ProjectUpdate, ProjectDelete,
        TaskCreate, TaskView, TaskUpdate, TaskDelete,
        OrderView,
        ReportView,
        AnalyticsView,
        ActivityView,
        NotificationView, NotificationUpdate,
        EmployeeView, EmployeeManage, AttendanceManage, LeaveManage,
        AnnouncementView, AnnouncementManage, PerformanceView, PerformanceManage,
        LeadView, DealView, TimeView, TimeManage,
        NalvaView, NalvaManage, NalvaApprove,
        StorefrontView, StorefrontManage
    };

    public static readonly IReadOnlySet<string> AccountantPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ExpenseCreate, ExpenseView, ExpenseUpdate, ExpenseDelete,
        ExpenseCategoryCreate, ExpenseCategoryView, ExpenseCategoryUpdate, ExpenseCategoryDelete,
        FinanceView, ReportView, AnalyticsView,
        InvoiceCreate, InvoiceView, InvoiceUpdate, InvoiceDelete,
        PaymentView, OrderView,
        NotificationView, NotificationUpdate,
        EmployeeSalary
    };

    public static readonly IReadOnlySet<string> SalesPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CustomerCreate, CustomerView, CustomerUpdate, CustomerDelete,
        OrderCreate, OrderView, OrderUpdate, OrderDelete,
        PaymentCreate, PaymentView, PaymentUpdate, PaymentDelete,
        InvoiceCreate, InvoiceView, InvoiceUpdate, InvoiceDelete,
        QuotationCreate, QuotationView, QuotationUpdate, QuotationDelete,
        LeadView, LeadManage, DealView, DealManage, ReturnManage,
        StorefrontView
    };

    public static readonly IReadOnlySet<string> InventoryManagerPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        InventoryView, InventoryUpdate, InventoryAdjust,
        ProductCreate, ProductView, ProductUpdate, ProductDelete,
        SupplierCreate, SupplierView, SupplierUpdate, SupplierDelete,
        PurchaseOrderCreate, PurchaseOrderView, PurchaseOrderUpdate, PurchaseOrderDelete,
        WarehouseView, WarehouseManage, FulfillmentManage
    };

    public static readonly IReadOnlySet<string> AgentPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AgentExecute,
        NotificationView,
        ActivityView,
        TaskView,
        ProjectView
    };

    public static IReadOnlySet<string> BuildOwnerPermissions()
    {
        var all = All.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        all.Remove(SystemAdminManage);
        return all;
    }

    public static IReadOnlySet<string> BuildAdminPermissions()
    {
        var all = BuildOwnerPermissions().ToHashSet(StringComparer.OrdinalIgnoreCase);
        all.Remove(SubscriptionManage);
        return all;
    }
}

public sealed record PermissionDefinition(
    string Name,
    string Code,
    string Description,
    string Category);

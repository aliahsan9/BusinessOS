namespace BusinessOS.Domain.Enums;

public static class ActivityEntityTypes
{
    public const string Customer = "Customer";
    public const string Project = "Project";
    public const string Task = "Task";
    public const string Invoice = "Invoice";
    public const string Expense = "Expense";
    public const string Settings = "Settings";
}

public static class ActivityActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string Completed = "Completed";
    public const string Generated = "Generated";
    public const string Paid = "Paid";
    public const string Login = "Login";
    public const string Logout = "Logout";
    public const string ProfileUpdated = "ProfileUpdated";
    public const string PasswordChanged = "PasswordChanged";
    public const string InvoiceCreated = "InvoiceCreated";
    public const string InvoicePaid = "InvoicePaid";
    public const string ExpenseAdded = "ExpenseAdded";
    public const string TaskCompleted = "TaskCompleted";
    public const string CustomerCreated = "CustomerCreated";
    public const string ProjectCreated = "ProjectCreated";
}

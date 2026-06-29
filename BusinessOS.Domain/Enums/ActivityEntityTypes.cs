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
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string Completed = "Completed";
    public const string Generated = "Generated";
    public const string Paid = "Paid";
}

namespace BusinessOS.Domain.Enums;

public static class NotificationTypes
{
    public const string Success = "Success";
    public const string Info = "Info";
    public const string Warning = "Warning";
    public const string Error = "Error";
    public const string System = "System";
    public const string Business = "Business";
    public const string Billing = "Billing";
    public const string Task = "Task";
    public const string Project = "Project";
    public const string Customer = "Customer";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Success, Info, Warning, Error, System, Business, Billing, Task, Project, Customer
    };
}

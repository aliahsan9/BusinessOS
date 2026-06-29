namespace BusinessOS.Application.Common.Authorization;

public static class RoleNames
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
    public const string Accountant = "Accountant";
    public const string Viewer = "Viewer";
    public const string Sales = "Sales";
    public const string InventoryManager = "InventoryManager";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Owner,
        Admin,
        Manager,
        Employee,
        Accountant,
        Viewer,
        Sales,
        InventoryManager
    };

    public static readonly IReadOnlySet<string> Protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Owner,
        Admin,
        Manager,
        Employee,
        Accountant,
        Viewer
    };
}

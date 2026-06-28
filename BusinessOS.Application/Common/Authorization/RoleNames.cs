namespace BusinessOS.Application.Common.Authorization;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Accountant = "Accountant";
    public const string Sales = "Sales";
    public const string InventoryManager = "InventoryManager";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Admin,
        Manager,
        Accountant,
        Sales,
        InventoryManager,
        Viewer
    };
}

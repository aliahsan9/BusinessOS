namespace BusinessOS.Application.Common.Authorization;

public static class PermissionPolicies
{
    public const string Prefix = "Permission:";

    public static string For(string permissionCode) => Prefix + permissionCode;
}

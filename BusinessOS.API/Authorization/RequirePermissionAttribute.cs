using BusinessOS.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace BusinessOS.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionCode)
        : base(PermissionPolicies.For(permissionCode))
    {
        PermissionCode = permissionCode;
    }

    public string PermissionCode { get; }
}

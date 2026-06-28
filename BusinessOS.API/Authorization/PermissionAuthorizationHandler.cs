using BusinessOS.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace BusinessOS.API.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var permissionsClaim = context.User.FindFirst(ClaimTypesConstants.Permissions)?.Value;
        if (string.IsNullOrWhiteSpace(permissionsClaim))
        {
            return Task.CompletedTask;
        }

        var permissions = permissionsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (permissions.Contains(requirement.PermissionCode, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

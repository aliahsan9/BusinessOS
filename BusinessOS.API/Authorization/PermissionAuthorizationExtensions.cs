using BusinessOS.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace BusinessOS.API.Authorization;

public static class PermissionAuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }

    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permissionCode) =>
        builder.RequireAuthorization(PermissionPolicies.For(permissionCode));

    public static RouteGroupBuilder RequirePermission(this RouteGroupBuilder builder, string permissionCode) =>
        builder.RequireAuthorization(PermissionPolicies.For(permissionCode));
}

using BusinessOS.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace BusinessOS.API.Authorization;

public static class DashboardAuthorizationExtensions
{
    public static IServiceCollection AddDashboardAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(DashboardPolicies.Overview, policy =>
                policy.RequireRole(
                    RoleNames.Admin,
                    RoleNames.Manager))
            .AddPolicy(DashboardPolicies.BusinessAnalytics, policy =>
                policy.RequireRole(
                    RoleNames.Admin,
                    RoleNames.Manager))
            .AddPolicy(DashboardPolicies.SalesReports, policy =>
                policy.RequireRole(
                    RoleNames.Admin,
                    RoleNames.Manager,
                    RoleNames.Sales))
            .AddPolicy(DashboardPolicies.InventoryAnalytics, policy =>
                policy.RequireRole(
                    RoleNames.Admin,
                    RoleNames.Manager,
                    RoleNames.InventoryManager));

        return services;
    }
}

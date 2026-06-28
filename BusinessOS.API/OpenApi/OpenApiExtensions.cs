using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BusinessOS.API.OpenApi;

public static class OpenApiExtensions
{
    public static IServiceCollection AddBusinessOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "BusinessOS API",
                    Version = "v1",
                    Description =
                        "Multi-tenant business management API with enterprise RBAC. " +
                        "Protected endpoints require a Bearer JWT (roles + permissions in claims) and the X-Tenant-ID header. " +
                        "Permission examples: Category.Create, Product.View, Order.Delete, Inventory.Adjust, Role.View. " +
                        "Default roles: Admin (all), Manager, Sales, InventoryManager, Viewer (read-only)."
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description =
                        "JWT bearer token. Claims include user id, email, tenant id, roles, and a compact " +
                        "'perm' claim with comma-separated permission codes."
                };

                return Task.CompletedTask;
            });
        });

        return services;
    }
}

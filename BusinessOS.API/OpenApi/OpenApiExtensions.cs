using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BusinessOS.API.OpenApi;

/// <summary>
/// Registers OpenAPI document generation with BusinessOS-specific metadata for Scalar.
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>
    /// Adds OpenAPI generation with JWT + tenant security schemes, tag ordering, and XML documentation support.
    /// </summary>
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
                        "Multi-tenant business management API with enterprise RBAC.\n\n" +
                        "## Authentication\n" +
                        "Most endpoints require a **Bearer JWT**. Obtain a token via `POST /api/auth/login` or `POST /api/auth/register`.\n\n" +
                        "## Multi-Tenancy\n" +
                        "Protected tenant-scoped endpoints require the **`X-Tenant-ID`** header (GUID). " +
                        "The value must match the `TenantId` claim embedded in the JWT. " +
                        "Data is isolated per tenant via EF Core global query filters.\n\n" +
                        "## Authorization\n" +
                        "Fine-grained permissions are enforced via policy codes (e.g. `Product.Create`, `Order.View`). " +
                        "Permissions are embedded in the JWT `perm` claim as comma-separated codes.\n\n" +
                        "## Default Roles\n" +
                        "Admin (all permissions), Manager, Sales, InventoryManager, Viewer (read-only).\n\n" +
                        "## Documentation\n" +
                        "Browse this reference at `/scalar`. Raw OpenAPI JSON is at `/openapi/v1.json`."
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description =
                        "JWT bearer token from `/api/auth/login` or `/api/auth/register`. " +
                        "Claims include user id, email, tenant id, roles, and a compact `perm` claim " +
                        "with comma-separated permission codes."
                };

                document.Components.SecuritySchemes["TenantHeader"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "X-Tenant-ID",
                    Description =
                        "Active tenant GUID. Required for authenticated tenant-scoped requests. " +
                        "Must match the TenantId claim in the JWT."
                };

                OrderTags(document);

                return Task.CompletedTask;
            });

            options.AddXmlDocumentationSupport(
                typeof(Program).Assembly,
                typeof(IApplicationDbContext).Assembly,
                typeof(Product).Assembly,
                typeof(BusinessOSDbContext).Assembly);

            options.AddOperationTransformer((operation, context, _) =>
            {
                var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
                    .Any(m => m is Microsoft.AspNetCore.Authorization.IAuthorizeData);

                var path = context.Description.RelativePath ?? string.Empty;
                var isPublicAuth = path.StartsWith("api/auth", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("api/health", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("api/register-business", StringComparison.OrdinalIgnoreCase);

                if (requiresAuth && !isPublicAuth)
                {
                    OpenApiDocumentationExtensions.ApplySecurityRequirements(operation, requiresTenantHeader: true);
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    private static void OrderTags(OpenApiDocument document)
    {
        if (document.Tags is null || document.Tags.Count == 0)
        {
            return;
        }

        var orderLookup = OpenApiDocumentationExtensions.TagOrder
            .Select((tag, index) => (tag, index))
            .ToDictionary(x => x.tag, x => x.index, StringComparer.OrdinalIgnoreCase);

        var sorted = document.Tags
            .OrderBy(t => orderLookup.TryGetValue(t.Name ?? string.Empty, out var index) ? index : int.MaxValue)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        document.Tags = new HashSet<OpenApiTag>(sorted);
    }
}

using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BusinessOS.API.OpenApi;

/// <summary>
/// Extension methods for enriching minimal API OpenAPI metadata from XML documentation and BusinessOS conventions.
/// </summary>
public static class OpenApiDocumentationExtensions
{
    /// <summary>
    /// Standard OpenAPI tag order for Scalar grouping.
    /// </summary>
    public static readonly string[] TagOrder =
    [
        "Authentication",
        "Users",
        "Roles",
        "Products",
        "Categories",
        "Suppliers",
        "Customers",
        "Purchases",
        "Sales",
        "Inventory",
        "Reports",
        "Dashboard",
        "AI",
        "Billing",
        "Notifications",
        "Settings",
        "Health"
    ];

    /// <summary>
    /// Registers operation and schema transformers that merge XML documentation into the OpenAPI document.
    /// </summary>
    public static OpenApiOptions AddXmlDocumentationSupport(this OpenApiOptions options, params Assembly[] assemblies)
    {
        var xmlProvider = new XmlDocumentationProvider(assemblies);

        options.AddOperationTransformer((operation, context, _) =>
        {
            EnrichOperationFromXml(operation, context, xmlProvider);
            AddStandardErrorResponses(operation);
            return Task.CompletedTask;
        });

        options.AddSchemaTransformer((schema, context, _) =>
        {
            if (context.JsonTypeInfo.Type is { } type)
            {
                var summary = xmlProvider.GetTypeSummary(type);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    schema.Description = summary;
                }
            }

            return Task.CompletedTask;
        });

        return options;
    }

    /// <summary>
    /// Applies BusinessOS security requirements (Bearer JWT + optional tenant header) to an operation.
    /// </summary>
    public static void ApplySecurityRequirements(
        OpenApiOperation operation,
        bool requiresAuthentication = true,
        bool requiresTenantHeader = true)
    {
        operation.Security ??= [];

        if (!requiresAuthentication)
        {
            return;
        }

        var requirements = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer")] = []
        };

        if (requiresTenantHeader)
        {
            requirements[new OpenApiSecuritySchemeReference("TenantHeader")] = [];
        }

        operation.Security.Add(requirements);
    }

    private static void EnrichOperationFromXml(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        XmlDocumentationProvider xmlProvider)
    {
        var method = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<MethodInfo>()
            .FirstOrDefault();

        if (method is null)
        {
            return;
        }

        var fullDescription = xmlProvider.GetFullDescription(XmlDocumentationProvider.GetMethodMemberName(method));
        if (!string.IsNullOrWhiteSpace(fullDescription))
        {
            operation.Description = fullDescription;
        }

        var summary = xmlProvider.GetMethodSummary(method);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            operation.Summary = summary;
        }
    }

    private static void AddStandardErrorResponses(OpenApiOperation operation)
    {
        operation.Responses ??= new OpenApiResponses();

        AddResponseIfMissing(operation.Responses, "401", "Unauthorized — missing or invalid JWT bearer token.");
        AddResponseIfMissing(operation.Responses, "403", "Forbidden — insufficient permissions or inactive/mismatched tenant.");
        AddResponseIfMissing(operation.Responses, "500", "Internal server error — unexpected failure.");
    }

    private static void AddResponseIfMissing(OpenApiResponses responses, string statusCode, string description)
    {
        if (!responses.ContainsKey(statusCode))
        {
            responses[statusCode] = new OpenApiResponse { Description = description };
        }
    }
}

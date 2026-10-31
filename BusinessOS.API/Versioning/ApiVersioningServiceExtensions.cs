using Asp.Versioning;
using BusinessOS.API.OpenApi;

namespace BusinessOS.API.Versioning;

/// <summary>
/// Registers URL-segment API versioning, API Explorer, and versioned OpenAPI documents.
/// </summary>
public static class ApiVersioningServiceExtensions
{
    public static IServiceCollection AddBusinessApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                // The version is part of the URL. Requests that omit it do not match a versioned route
                // and are rejected by UnversionedApiPathMiddleware. Do not silently serve v1 at /api/{resource}.
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
                options.SubstitutionFormat = "VVV";
            })
            .AddBusinessOpenApi();

        return services;
    }
}

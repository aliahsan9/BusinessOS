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
                        "Multi-tenant business management API for authentication, categories, and products. " +
                        "Protected endpoints require a Bearer JWT and the X-Tenant-ID header."
                };

                return Task.CompletedTask;
            });
        });

        return services;
    }
}

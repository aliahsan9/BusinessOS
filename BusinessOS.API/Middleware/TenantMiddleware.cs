using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.MultiTenancy;
using System.Text.Json;

namespace BusinessOS.API.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        // Allow swagger/scalar endpoints without tenant (optional safety)
        var path = context.Request.Path.Value?.ToLower();

        if (path != null &&
            (path.Contains("swagger") || path.Contains("scalar")))
        {
            await _next(context);
            return;
        }

        // Validate header
        if (!context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId) ||
            !Guid.TryParse(tenantId, out var parsedTenantId))
        {
            await WriteJsonResponse(context, StatusCodes.Status400BadRequest, new
            {
                error = "Missing or invalid X-Tenant-ID header",
                code = "TENANT_HEADER_REQUIRED"
            });

            return;
        }

        // Set tenant
        tenantProvider.SetTenantId(parsedTenantId);

        await _next(context);
    }

    private static async Task WriteJsonResponse(HttpContext context, int statusCode, object response)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(response);

        await context.Response.WriteAsync(json);
    }
}

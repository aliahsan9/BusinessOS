using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.MultiTenancy;
using System.Text.Json;

namespace BusinessOS.API.Middleware;

public sealed class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Allow Swagger / OpenAPI endpoints
        if (path.StartsWith("/swagger") ||
            path.StartsWith("/scalar") ||
            path.StartsWith("/openapi"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantHeader) ||
            !Guid.TryParse(tenantHeader, out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new
                {
                    error = "Missing or invalid X-Tenant-ID header",
                    code = "TENANT_HEADER_REQUIRED"
                }));

            return;
        }

        try
        {
            tenantProvider.SetTenantId(tenantId);

            await _next(context);
        }
        finally
        {
            TenantContext.Clear();
        }
    }
}

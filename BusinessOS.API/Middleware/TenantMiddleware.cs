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
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // ✅ ALWAYS allow API documentation tools
        if (path.StartsWith("/swagger") ||
            path.StartsWith("/scalar") ||
            path.StartsWith("/openapi"))
        {
            await _next(context);
            return;
        }

        // ✅ Validate tenant header ONLY for API calls
        if (!context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId) ||
            !Guid.TryParse(tenantId, out var parsedTenantId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var error = JsonSerializer.Serialize(new
            {
                error = "Missing or invalid X-Tenant-ID header",
                code = "TENANT_HEADER_REQUIRED"
            });

            await context.Response.WriteAsync(error);
            return;
        }

        // ✅ Set tenant
        tenantProvider.SetTenantId(parsedTenantId);

        await _next(context);
    }
}

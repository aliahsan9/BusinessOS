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

        if (path.StartsWith("/swagger") ||
            path.StartsWith("/scalar") ||
            path.StartsWith("/openapi") ||
            path.StartsWith("/api/auth"))
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantHeader) &&
            Guid.TryParse(tenantHeader, out var tenantId))
        {
            tenantProvider.SetTenantId(tenantId);
            await _next(context);
            return;
        }

        var tenantClaim = context.User.FindFirst("TenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantClaim) &&
            Guid.TryParse(tenantClaim, out var claimTenantId))
        {
            tenantProvider.SetTenantId(claimTenantId);
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                title = "Missing or invalid X-Tenant-ID header",
                status = 400,
                code = "TENANT_HEADER_REQUIRED"
            }));

        TenantContext.Clear();
    }
}

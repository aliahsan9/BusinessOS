using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.MultiTenancy;

namespace BusinessOS.API.Middlewares;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        // OPTION 1: From Header (simple for MVP)
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
        {
            tenantProvider.SetTenantId(Guid.Parse(tenantId!));
        }

        // OPTION 2: Later from JWT claim (recommended for production)

        await _next(context);
    }
}

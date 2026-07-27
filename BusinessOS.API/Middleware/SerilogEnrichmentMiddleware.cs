using System.Security.Claims;
using BusinessOS.Application.Common.Interfaces;
using Serilog.Context;

namespace BusinessOS.API.Middleware;

/// <summary>
/// Pushes tenant and user properties into the Serilog log context after authentication
/// and tenant resolution. Does not alter tenant resolution behavior.
/// </summary>
public sealed class SerilogEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public SerilogEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        ITenantContext tenantContext)
    {
        var disposables = new List<IDisposable>(4);

        try
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                disposables.Add(LogContext.PushProperty("UserId", userId));
            }

            if (tenantProvider.HasTenant())
            {
                disposables.Add(LogContext.PushProperty("TenantId", tenantProvider.TenantId));

                if (tenantContext.IsLoaded && !string.IsNullOrWhiteSpace(tenantContext.TenantName))
                {
                    disposables.Add(LogContext.PushProperty("TenantName", tenantContext.TenantName));
                }
            }

            await _next(context);
        }
        finally
        {
            for (var i = disposables.Count - 1; i >= 0; i--)
            {
                disposables[i].Dispose();
            }
        }
    }
}

using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BusinessOS.API.Middleware;

public sealed class TenantMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantProvider tenantProvider,
        ITenantContext tenantContext)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        if (IsExcludedPath(path))
        {
            await _next(context);
            return;
        }

        Guid? resolvedTenantId = null;

        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantHeader) &&
            Guid.TryParse(tenantHeader, out var headerTenantId))
        {
            resolvedTenantId = headerTenantId;
        }
        else
        {
            var tenantClaim = context.User.FindFirst("TenantId")?.Value;
            if (!string.IsNullOrWhiteSpace(tenantClaim) &&
                Guid.TryParse(tenantClaim, out var claimTenantId))
            {
                resolvedTenantId = claimTenantId;
            }
        }

        if (resolvedTenantId.HasValue)
        {
            tenantProvider.SetTenantId(resolvedTenantId.Value);
            TenantContext.SetTenantId(resolvedTenantId.Value);

            if (context.User.Identity?.IsAuthenticated == true)
            {
                await tenantContext.LoadAsync(context.RequestAborted);

                if (tenantContext.IsLoaded && !tenantContext.IsActive)
                {
                    await WriteProblemAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        "TENANT_INACTIVE",
                        "Tenant account is inactive or suspended.");

                    TenantContext.Clear();
                    return;
                }

                if (tenantContext.IsLoaded &&
                    context.User.Identity?.IsAuthenticated == true)
                {
                    var claimTenant = context.User.FindFirst("TenantId")?.Value;
                    if (!string.IsNullOrWhiteSpace(claimTenant) &&
                        Guid.TryParse(claimTenant, out var jwtTenantId) &&
                        jwtTenantId != resolvedTenantId.Value)
                    {
                        _logger.LogWarning(
                            "Tenant mismatch: JWT {JwtTenant} vs resolved {ResolvedTenant}",
                            jwtTenantId,
                            resolvedTenantId.Value);

                        await WriteProblemAsync(
                            context,
                            StatusCodes.Status403Forbidden,
                            "TENANT_MISMATCH",
                            "Tenant context does not match authenticated user.");

                        TenantContext.Clear();
                        return;
                    }
                }
            }

            await _next(context);
            TenantContext.Clear();
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        _logger.LogWarning(
            "Authenticated request missing tenant context for {Path} (TraceId: {TraceId})",
            context.Request.Path,
            context.TraceIdentifier);

        await WriteProblemAsync(
            context,
            StatusCodes.Status400BadRequest,
            "TENANT_HEADER_REQUIRED",
            "Missing or invalid X-Tenant-ID header");

        TenantContext.Clear();
    }

    private static bool IsExcludedPath(string path) =>
        path.StartsWith("/swagger") ||
        path.StartsWith("/scalar") ||
        path.StartsWith("/openapi") ||
        path.StartsWith("/api/health") ||
        path.StartsWith("/api/auth") ||
        path.StartsWith("/api/register-business");

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string code,
        string title)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = title,
            Status = statusCode,
            Instance = context.Request.Path
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}

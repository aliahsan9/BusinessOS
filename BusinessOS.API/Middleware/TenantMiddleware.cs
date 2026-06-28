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
        ITenantProvider tenantProvider)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        if (path.StartsWith("/swagger") ||
            path.StartsWith("/scalar") ||
            path.StartsWith("/openapi") ||
            path.StartsWith("/api/health") ||
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

        _logger.LogWarning(
            "Authenticated request missing tenant context for {Path} (TraceId: {TraceId})",
            context.Request.Path,
            context.TraceIdentifier);

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "Missing or invalid X-Tenant-ID header",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.Request.Path
        };

        problem.Extensions["code"] = "TENANT_HEADER_REQUIRED";
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));

        TenantContext.Clear();
    }
}

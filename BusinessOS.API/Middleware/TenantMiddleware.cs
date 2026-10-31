using System.Security.Claims;
using BusinessOS.API.Versioning;
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
        ITenantContext tenantContext,
        ITenantMembershipLookup membershipLookup)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        if (IsExcludedPath(path))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            // Anonymous routes must not adopt a client-supplied tenant.
            await _next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "AUTH_REQUIRED",
                "Authentication is required.");
            return;
        }

        var membership = await membershipLookup.FindByUserIdAsync(userId, context.RequestAborted);
        if (membership is null || !membership.IsActive)
        {
            _logger.LogWarning(
                "Rejected inactive or unknown account {UserId} for {Path}",
                userId,
                context.Request.Path);

            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "ACCOUNT_INACTIVE",
                "Account is deactivated.");
            return;
        }

        if (TryGetHeaderTenant(context, out var headerTenantId) &&
            headerTenantId != membership.TenantId)
        {
            _logger.LogWarning(
                "Rejected spoofed X-Tenant-ID {HeaderTenant} for user {UserId} who belongs to {MembershipTenant}",
                headerTenantId,
                userId,
                membership.TenantId);

            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "TENANT_MISMATCH",
                "Tenant context does not match authenticated user.");
            return;
        }

        var claimTenant = context.User.FindFirst("TenantId")?.Value;
        if (!Guid.TryParse(claimTenant, out var jwtTenantId) || jwtTenantId != membership.TenantId)
        {
            _logger.LogWarning(
                "Rejected JWT tenant {JwtTenant} that does not match membership {MembershipTenant} for user {UserId}",
                claimTenant,
                membership.TenantId,
                userId);

            await WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "TENANT_MISMATCH",
                "Tenant context does not match authenticated user.");
            return;
        }

        tenantProvider.SetTenantId(membership.TenantId);
        TenantContext.SetTenantId(membership.TenantId);

        try
        {
            await tenantContext.LoadAsync(context.RequestAborted);

            if (!tenantContext.IsLoaded)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "TENANT_NOT_FOUND",
                    "Tenant account was not found.");
                return;
            }

            if (!tenantContext.IsActive)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "TENANT_INACTIVE",
                    "Tenant account is inactive or suspended.");
                return;
            }

            await _next(context);
        }
        finally
        {
            TenantContext.Clear();
        }
    }

    private static bool TryGetHeaderTenant(HttpContext context, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        return context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantHeader) &&
               Guid.TryParse(tenantHeader, out tenantId);
    }

    private static bool IsExcludedPath(string path)
    {
        if (path.StartsWith("/swagger") ||
            path.StartsWith("/scalar") ||
            path.StartsWith("/openapi") ||
            path.StartsWith("/api/health"))
        {
            return true;
        }

        var resourcePath = ApiVersioningPaths.StripVersion(path);
        return resourcePath.StartsWith("/api/auth") ||
               resourcePath.StartsWith("/api/register-business");
    }

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

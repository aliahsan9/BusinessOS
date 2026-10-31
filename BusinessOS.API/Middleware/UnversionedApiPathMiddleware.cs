using System.Text.Json;
using BusinessOS.API.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.API.Middleware;

/// <summary>
/// Turns a missing URL version into 400 <c>API_VERSION_REQUIRED</c>, and an unknown URL version into 400 <c>API_VERSION_UNSUPPORTED</c>.
/// The version route constraint does not select an endpoint for an undeclared version, which would otherwise be a generic 404.
/// </summary>
public sealed class UnversionedApiPathMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;

    public UnversionedApiPathMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.HasStarted ||
            context.Response.StatusCode != StatusCodes.Status404NotFound)
        {
            return;
        }

        var path = context.Request.Path.Value ?? "/api";

        if (ApiVersioningPaths.IsUnversionedBusinessPath(context.Request.Path))
        {
            var versionedPath = "/api/v1" + path["/api".Length..];
            await WriteProblemAsync(
                context,
                "API version is required",
                $"This API requires an explicit version in the URL. Use '{versionedPath}'.",
                ApiVersioningPaths.VersionRequiredCode);
            return;
        }

        if (ApiVersioningPaths.TryGetRequestedVersion(path, out var requested) &&
            !ApiVersioningPaths.IsSupportedVersion(requested))
        {
            await WriteProblemAsync(
                context,
                "Unsupported API version",
                $"The requested API version '{requested}' is not supported.",
                ApiVersioningPaths.VersionUnsupportedCode);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        string title,
        string detail,
        string code)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers[ApiVersioningPaths.SupportedVersionsHeader] = ApiVersioningPaths.CurrentVersion;

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = title,
            Status = StatusCodes.Status400BadRequest,
            Detail = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}

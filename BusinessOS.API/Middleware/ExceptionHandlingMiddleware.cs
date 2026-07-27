using System.Security.Claims;
using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BusinessOS.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context) ?? traceId;
        var endpoint = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "unknown";
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? tenantId = null;

        var tenantProvider = context.RequestServices.GetService<ITenantProvider>();
        if (tenantProvider?.HasTenant() == true)
        {
            tenantId = tenantProvider.TenantId;
        }

        var (statusCode, title, type, errors, logLevel) = exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                validationException.Errors
                    .GroupBy(x => x.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.ErrorMessage).ToArray()),
                LogLevel.Warning),

            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                notFound.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                (Dictionary<string, string[]>?)null,
                LogLevel.Warning),

            UnauthorizedException unauthorized => (
                StatusCodes.Status401Unauthorized,
                unauthorized.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                null,
                LogLevel.Warning),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                conflict.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                null,
                LogLevel.Warning),

            BadRequestException badRequest => (
                StatusCodes.Status400BadRequest,
                badRequest.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                null,
                LogLevel.Warning),

            BadHttpRequestException badHttpRequest => (
                StatusCodes.Status400BadRequest,
                "Invalid request body",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                null,
                LogLevel.Warning),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                null,
                LogLevel.Error)
        };

        _logger.Log(
            logLevel,
            exception,
            "Unhandled exception {ExceptionType} at {Endpoint} with status {StatusCode}. Message={Message}. RequestId={RequestId}, CorrelationId={CorrelationId}, UserId={UserId}, TenantId={TenantId}",
            exception.GetType().FullName,
            endpoint,
            statusCode,
            exception.Message,
            traceId,
            correlationId,
            userId,
            tenantId);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type = type,
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError && !_environment.IsDevelopment()
                ? title
                : exception.Message,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = traceId;
        problem.Extensions["correlationId"] = correlationId;

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}

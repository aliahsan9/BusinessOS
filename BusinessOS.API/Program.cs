using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using BusinessOS.API.Authorization;
using BusinessOS.API.Hubs;
using BusinessOS.API.Middleware;
using BusinessOS.API.Services;
using BusinessOS.API.Versioning;
using BusinessOS.Application;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Infrastructure;
using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.Configure<LoggingPerformanceOptions>(
        builder.Configuration.GetSection(LoggingPerformanceOptions.SectionName));

    builder.Services.Configure<CacheSettings>(
        builder.Configuration.GetSection(CacheSettings.SectionName));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.Configure<BusinessOS.Application.Features.Dashboard.Services.DashboardCacheOptions>(
        builder.Configuration.GetSection(BusinessOS.Application.Features.Dashboard.Services.DashboardCacheOptions.SectionName));
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddProblemDetails();
    // Adding Rate Limiting services

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        {
            if (IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }
    });

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 5 * 1024 * 1024;
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddFixedWindowLimiter("api", limiterOptions =>
        {
            limiterOptions.PermitLimit = 100;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
        });

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        options.AddPolicy("ai", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();
    builder.Services.AddScoped<IRealtimeNotificationService, SignalRRealtimeNotificationService>();
    builder.Services.AddBusinessApiVersioning();
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

    var jwtIssuer = builder.Configuration["Jwt:Issuer"];
    var jwtAudience = builder.Configuration["Jwt:Audience"];
    var jwtKey = builder.Configuration["Jwt:Key"];
    JwtConfigurationGuard.Ensure(jwtIssuer, jwtAudience, jwtKey);

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs/notifications"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtAuthentication");

                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        logger.LogWarning(
                            "Expired JWT rejected for {Path} (RequestId={RequestId})",
                            context.HttpContext.Request.Path,
                            context.HttpContext.TraceIdentifier);
                    }
                    else
                    {
                        logger.LogWarning(
                            "JWT authentication failed for {Path}. Reason={Reason} (RequestId={RequestId})",
                            context.HttpContext.Request.Path,
                            context.Exception.GetType().Name,
                            context.HttpContext.TraceIdentifier);
                    }

                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    if (!string.IsNullOrEmpty(context.Error))
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuthentication");

                        logger.LogWarning(
                            "Unauthorized access to {Path}: {Error} {ErrorDescription} (RequestId={RequestId})",
                            context.HttpContext.Request.Path,
                            context.Error,
                            context.ErrorDescription,
                            context.HttpContext.TraceIdentifier);
                    }

                    return Task.CompletedTask;
                },
                OnForbidden = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtAuthentication");

                    logger.LogWarning(
                        "Forbidden access to {Path} by User {UserId} (RequestId={RequestId})",
                        context.HttpContext.Request.Path,
                        context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                        context.HttpContext.TraceIdentifier);

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddPermissionAuthorization();
    builder.Services.AddDashboardAuthorization();

    const string corsPolicyName = "BusinessCorsPolicy";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(corsPolicyName, policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .WithExposedHeaders("X-Tts-Provider", "X-Tts-Voice", "X-Tts-Cache", "X-CSRF-Token");
            }
        });
    });

    var app = builder.Build();

    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    {
        ProductionSecurityGuard.Ensure(app.Configuration);
    }

    await DbInitializer.SeedAsync(app.Services);

    app.UseForwardedHeaders();

    // Correlation ID must run first so all subsequent logs share it.
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex is not null || httpContext.Response.StatusCode >= 500)
            {
                return LogEventLevel.Error;
            }

            if (httpContext.Response.StatusCode >= 400)
            {
                return LogEventLevel.Warning;
            }

            var threshold = httpContext.RequestServices
                .GetService<Microsoft.Extensions.Options.IOptionsMonitor<LoggingPerformanceOptions>>()
                ?.CurrentValue.HttpWarningThresholdMs ?? 3000;

            if (threshold > 0 && elapsed >= threshold)
            {
                return LogEventLevel.Warning;
            }

            return LogEventLevel.Information;
        };

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
            diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value);
            diagnosticContext.Set("StatusCode", httpContext.Response.StatusCode);
            diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
            diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
            diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);

            var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext);
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                diagnosticContext.Set("CorrelationId", correlationId);
            }

            var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                diagnosticContext.Set("UserId", userId);
            }

            var tenantProvider = httpContext.RequestServices.GetService<ITenantProvider>();
            if (tenantProvider?.HasTenant() == true)
            {
                diagnosticContext.Set("TenantId", tenantProvider.TenantId);
            }

            var tenantContext = httpContext.RequestServices.GetService<ITenantContext>();
            if (tenantContext?.IsLoaded == true && !string.IsNullOrWhiteSpace(tenantContext.TenantName))
            {
                diagnosticContext.Set("TenantName", tenantContext.TenantName);
            }
        };

        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms (RemoteIP={RemoteIP}, RequestId={RequestId}, TraceId={TraceId}, CorrelationId={CorrelationId})";
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<UnversionedApiPathMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        headers["Content-Security-Policy"] = "frame-ancestors 'none'";
        await next();
    });

    app.UseCors(corsPolicyName);

    // Keep rate limiting enabled everywhere except the automated test environment.
    // Development API testing (Scalar/Postman/Swagger) will still be rate limited.
    if (!app.Environment.IsEnvironment("Testing"))
    {
        app.UseRateLimiter();
    }

    app.UseAuthentication();
    app.UseMiddleware<TenantMiddleware>();
    app.UseMiddleware<SerilogEnrichmentMiddleware>();
    app.UseAuthorization();

    app.MapHealthChecks("/api/health/ready")
        .WithTags("Health");
    app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", service = "BusinessOS.API" }))
        .WithTags("Health")
        .WithName("HealthCheck");

    app.MapBusinessApi();

    app.MapHub<NotificationHub>("/hubs/notifications")
        .RequireAuthorization();

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        app.MapBusinessApiReference();
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;

file static class ProductionSecurityGuard
{
    public static void Ensure(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin => origin is "*" or "null"))
        {
            throw new InvalidOperationException(
                "Production requires Cors:AllowedOrigins to list explicit https origins.");
        }

        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
        {
            throw new InvalidOperationException(
                "Production requires an explicit AllowedHosts value.");
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        if (connectionString.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Production database connections must encrypt traffic. Remove Encrypt=False.");
        }
    }
}

file static class JwtConfigurationGuard
{
    private const int MinimumKeyBytes = 32;

    private static readonly HashSet<string> ForbiddenKeys = new(StringComparer.Ordinal)
    {
        "THIS_IS_SUPER_SECRET_KEY_123456789",
        "super-secret-development-key-1234567890",
        "CHANGE_ME",
        "changeme"
    };

    public static void Ensure(string? issuer, string? audience, string? key)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                "Jwt:Issuer and Jwt:Audience are required.");
        }

        if (string.IsNullOrWhiteSpace(key) ||
            Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes ||
            ForbiddenKeys.Contains(key))
        {
            throw new InvalidOperationException(
                "Jwt:Key is missing, too short, or a known insecure placeholder. " +
                "Set a random key of at least 32 bytes via user secrets in Development " +
                "(dotnet user-secrets set \"Jwt:Key\" \"<key>\") or the Jwt__Key environment variable.");
        }
    }
}

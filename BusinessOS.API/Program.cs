using System.Text;
using System.Text.Json;
using BusinessOS.API.Authorization;
using BusinessOS.API.Endpoints;
using BusinessOS.API.Hubs;
using BusinessOS.API.Middleware;
using BusinessOS.API.OpenApi;
using BusinessOS.API.Services;
using BusinessOS.Application;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Infrastructure;
using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
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

    builder.Services.AddControllers();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.Configure<BusinessOS.Application.Features.Dashboard.Services.DashboardCacheOptions>(
        builder.Configuration.GetSection(BusinessOS.Application.Features.Dashboard.Services.DashboardCacheOptions.SectionName));
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddProblemDetails();
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();
    builder.Services.AddScoped<IRealtimeNotificationService, SignalRRealtimeNotificationService>();
    builder.Services.AddBusinessOpenApi();
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key is missing.");

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
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs"))
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
                            context.Exception,
                            "JWT authentication failed for {Path} (RequestId={RequestId})",
                            context.HttpContext.Request.Path,
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
                    .AllowCredentials();
            }
        });
    });

    var app = builder.Build();

    await DbInitializer.SeedAsync(app.Services);

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();

        app.MapScalarApiReference(options =>
        {
            options.Title = "BusinessOS API";
            options.Theme = ScalarTheme.BluePlanet;
            options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
            options.Authentication = new()
            {
                PreferredSecuritySchemes = ["Bearer", "TenantHeader"]
            };
            options.ShowSidebar = true;
        });
    }

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
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    app.UseCors(corsPolicyName);
    app.UseAuthentication();
    app.UseMiddleware<TenantMiddleware>();
    app.UseMiddleware<SerilogEnrichmentMiddleware>();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/api/health/ready");
    app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", service = "BusinessOS.API" }))
        .WithTags("Health")
        .WithName("HealthCheck");
    app.MapAuthEndpoints();
    app.MapAccountEndpoints();
    app.MapCategoryEndpoints();
    app.MapProductEndpoints();
    app.MapCustomerEndpoints();
    app.MapOrderEndpoints();
    app.MapSupplierEndpoints();
    app.MapPurchaseOrderEndpoints();
    app.MapPaymentEndpoints();
    app.MapInvoiceEndpoints();
    app.MapQuotationEndpoints();
    app.MapInventoryEndpoints();
    app.MapDashboardEndpoints();
    app.MapAnalyticsEndpoints();
    app.MapReportEndpoints();
    app.MapRoleEndpoints();
    app.MapExpenseEndpoints();
    app.MapFinanceEndpoints();
    app.MapUserEndpoints();
    app.MapAuditEndpoints();
    app.MapNotificationEndpoints();
    app.MapActivityEndpoints();
    app.MapSettingsEndpoints();
    app.MapOnboardingEndpoints();
    app.MapAiEndpoints();
    app.MapAgentEndpoints();
    app.MapHelpEndpoints();
    app.MapTeamEndpoints();
    app.MapOrganizationEndpoints();
    app.MapTenantEndpoints();
    app.MapBillingEndpoints();
    app.MapBusinessRegistrationEndpoint();
    app.MapSystemAdminEndpoints();
    app.MapHub<NotificationHub>("/hubs/notifications")
        .RequireAuthorization();

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

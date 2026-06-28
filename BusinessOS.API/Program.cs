using System.Text;
using System.Text.Json;
using BusinessOS.API.Authorization;
using BusinessOS.API.Endpoints;
using BusinessOS.API.Middleware;
using BusinessOS.API.OpenApi;
using BusinessOS.Application;
using BusinessOS.Infrastructure;
using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddControllers();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.Configure<BusinessOS.Application.Features.Dashboard.Services.DashboardCacheOptions>(
        builder.Configuration.GetSection(BusinessOS.Application.Features.Dashboard.Services.DashboardCacheOptions.SectionName));
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddProblemDetails();
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
                PreferredSecuritySchemes = ["Bearer"]
            };
        });
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHttpsRedirection();
    app.UseCors(corsPolicyName);
    app.UseAuthentication();
    app.UseMiddleware<TenantMiddleware>();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", service = "BusinessOS.API" }))
        .WithTags("Health")
        .WithName("HealthCheck");
    app.MapAuthEndpoints();
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
    app.MapRoleEndpoints();
    app.MapExpenseEndpoints();
    app.MapFinanceEndpoints();
    app.MapUserEndpoints();
    app.MapAuditEndpoints();
    app.MapNotificationEndpoints();
    app.MapSettingsEndpoints();
    app.MapSystemAdminEndpoints();

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

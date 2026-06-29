using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.Repositories;
using BusinessOS.Application.Features.Audit.Services;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Application.Features.Analytics.Services;
using BusinessOS.Application.Features.Finance.Services;
using BusinessOS.Application.Features.Pdf.Services;
using BusinessOS.Application.Features.Reports.Services;
using BusinessOS.Application.Features.Activities.Services;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Application.Features.Onboarding.Services;
using BusinessOS.Application.Features.AI.Services;
using BusinessOS.Application.Features.Help.Services;
using BusinessOS.Application.Features.Roles.Services;
using BusinessOS.Application.Features.Settings.Services;
using BusinessOS.Application.Features.SystemAdmin.Services;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ITenantDbConnection, TenantDbConnection>();

        void ConfigureOptions(DbContextOptionsBuilder options)
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                options.UseInMemoryDatabase(configuration["InMemoryDatabaseName"] ?? "BusinessOS_Test");
                return;
            }

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is not configured.");

            options.UseSqlServer(connectionString);
        }

        services.AddDbContext<BusinessOSDbContext>((_, options) => ConfigureOptions(options));
        services.AddDbContextFactory<BusinessOSDbContext>(
            (_, options) => ConfigureOptions(options),
            ServiceLifetime.Scoped);

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<BusinessOSDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<BusinessOSDbContext>());

        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IStockTransactionRepository, StockTransactionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantRegistrationService, TenantRegistrationService>();
        services.AddScoped<IUserAnalyticsService, UserAnalyticsService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRbacAuditService, RbacAuditService>();
        services.AddScoped<IAnalyticsModuleService, AnalyticsModuleService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IPdfGenerationService, PdfGenerationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IBusinessEventService, BusinessEventService>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();
        services.AddScoped<IRealtimeNotificationService, NullRealtimeNotificationService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ISystemAdminService, SystemAdminService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IAiAssistantService, AiAssistantService>();
        services.AddScoped<IHelpService, HelpService>();

        return services;
    }
}

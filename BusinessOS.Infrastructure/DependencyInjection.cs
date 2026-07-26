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
using BusinessOS.Application.Features.Organization.Services;
using BusinessOS.Application.Features.Roles.Services;
using BusinessOS.Application.Features.Team.Services;
using BusinessOS.Application.Features.Settings.Services;
using BusinessOS.Application.Features.SystemAdmin.Services;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.VectorSearch.Options;
using BusinessOS.Application.Features.VectorSearch.Services;
using BusinessOS.Infrastructure.AI;
using BusinessOS.Infrastructure.AI.Copilot;
using BusinessOS.Infrastructure.AI.Copilot.Tools;
using BusinessOS.Infrastructure.Payments;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using BusinessOS.Infrastructure.VectorSearch;
using BusinessOS.Infrastructure.VectorSearch.Projectors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;

namespace BusinessOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ITenantProvider, TenantProvider>();
        services.AddScoped<ITenantContext, TenantContextService>();
        services.AddScoped<ISuperAdminContext, SuperAdminContext>();
        services.AddScoped<ITenantDbConnection, TenantDbConnection>();

        services.Configure<QdrantOptions>(configuration.GetSection(QdrantOptions.SectionName));
        services.Configure<VectorSyncOptions>(configuration.GetSection(VectorSyncOptions.SectionName));

        services.AddSingleton<IVectorEntityProjector, DocumentVectorProjector>();
        services.AddSingleton<IVectorEntityProjector, ProductVectorProjector>();
        services.AddSingleton<IVectorEntityProjector, ProjectVectorProjector>();
        services.AddSingleton<IVectorEntityProjector, CustomerVectorProjector>();
        services.AddSingleton<IVectorEntityProjectorRegistry, VectorEntityProjectorRegistry>();
        services.AddSingleton<VectorSyncOutboxInterceptor>();

        void ConfigureOptions(IServiceProvider sp, DbContextOptionsBuilder options)
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                options.UseInMemoryDatabase(configuration["InMemoryDatabaseName"] ?? "BusinessOS_Test");
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("DefaultConnection is not configured.");

                options.UseSqlServer(connectionString);
            }

            options.AddInterceptors(sp.GetRequiredService<VectorSyncOutboxInterceptor>());
        }

        services.AddDbContext<BusinessOSDbContext>((sp, options) => ConfigureOptions(sp, options));
        services.AddDbContextFactory<BusinessOSDbContext>(
            (sp, options) => ConfigureOptions(sp, options),
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
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantLimitService, TenantLimitService>();
        services.AddScoped<ITenantAuditService, TenantAuditService>();
        services.AddScoped<IUserAnalyticsService, UserAnalyticsService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRbacAuditService, RbacAuditService>();
        services.AddScoped<IAnalyticsModuleService, AnalyticsModuleService>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<IPdfGenerationService, PdfGenerationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEntityAuditService, EntityAuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IBusinessEventService, BusinessEventService>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();
        services.AddScoped<IRealtimeNotificationService, NullRealtimeNotificationService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ISystemAdminService, SystemAdminService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IAiContextService, AiContextService>();
        services.AddScoped<IAiRetrievalService, AiRetrievalService>();
        services.AddScoped<IAiActionService, AiActionService>();
        services.AddScoped<IAiPromptBuilder, AiPromptBuilder>();
        services.AddScoped<IAiChatService, AiChatService>();
        services.AddScoped<IAiIntentDetector, AiIntentDetector>();
        services.AddScoped<IAiPermissionValidator, AiPermissionValidator>();
        services.AddScoped<IAiMemoryService, AiMemoryService>();
        services.AddScoped<IAiAnalyticsQueryService, AiAnalyticsQueryService>();
        services.AddScoped<IAiVectorRagService, AiVectorRagService>();
        services.AddScoped<IAiObservabilityService, AiObservabilityService>();
        services.AddScoped<IAiInsightService, AiInsightService>();
        services.AddScoped<IAiCopilotOrchestrator, AiCopilotOrchestrator>();
        services.AddScoped<IAiAssistantService, AiAssistantService>();
        services.AddScoped<IAiTool, GetCustomersTool>();
        services.AddScoped<IAiTool, GetProjectsTool>();
        services.AddScoped<IAiTool, GetTasksTool>();
        services.AddScoped<IAiTool, GetInvoicesTool>();
        services.AddScoped<IAiTool, GetExpensesTool>();
        services.AddScoped<IAiTool, GetProductsTool>();
        services.AddScoped<IAiTool, GetRevenueTool>();
        services.AddScoped<IAiTool, GetSalesSummaryTool>();
        services.AddScoped<IAiTool, GetBestSellingProductsTool>();
        services.AddScoped<IAiTool, GetSalesTrendsTool>();
        services.AddScoped<IAiTool, CreateCustomerTool>();
        services.AddScoped<IAiTool, CreateProjectTool>();
        services.AddScoped<IAiTool, CreateTaskTool>();
        services.AddScoped<IAiTool, CreateInvoiceTool>();
        services.AddScoped<IAiTool, SearchDocumentsTool>();
        services.AddScoped<IAiToolRegistry, AiToolRegistry>();
        services.AddScoped<IHelpService, HelpService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IOrganizationService, OrganizationService>();

        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.Configure<JazzCashOptions>(configuration.GetSection(JazzCashOptions.SectionName));
        services.Configure<EasyPaisaOptions>(configuration.GetSection(EasyPaisaOptions.SectionName));
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        services.AddHttpClient<OpenAiChatClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(options.OpenAiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        services.AddHttpClient<CursorLlmChatClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        services.AddHttpClient<OpenAiEmbeddingClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(options.OpenAiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        services.AddScoped<ILlmChatClient, LlmChatClientRouter>();

        RegisterVectorSearch(services);

        services.AddScoped<BillingService>();
        services.AddScoped<IBillingService>(sp => sp.GetRequiredService<BillingService>());
        services.AddScoped<IBillingPlanSyncService>(sp => sp.GetRequiredService<BillingService>());
        services.AddScoped<Func<IBillingPlanSyncService>>(sp => () => sp.GetRequiredService<IBillingPlanSyncService>());
        services.AddScoped<IBillingWebhookService, BillingWebhookService>();
        services.AddScoped<IBillingMetricsService, BillingMetricsService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IJazzCashPaymentService, JazzCashPaymentService>();
        services.AddScoped<IEasyPaisaPaymentService, EasyPaisaPaymentService>();

        return services;
    }

    private static void RegisterVectorSearch(IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantOptions>>().Value;
            return new QdrantClient(
                host: options.Host,
                port: options.Port,
                https: options.Https,
                apiKey: string.IsNullOrWhiteSpace(options.ApiKey) ? null : options.ApiKey);
        });

        services.AddSingleton<IVectorStore, QdrantVectorStore>();
        services.AddScoped<IEmbeddingGenerator, OpenAiEmbeddingGenerator>();
        services.AddScoped<IVectorSearchService, VectorSearchService>();
        services.AddScoped<IVectorSyncOutboxWriter, VectorSyncOutboxWriter>();
        services.AddHostedService<VectorSyncBackgroundService>();
        services.AddHostedService<VectorBackfillHostedService>();

        services.AddHealthChecks()
            .AddCheck<QdrantHealthCheck>("qdrant");
    }
}

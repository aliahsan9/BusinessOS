using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
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

        services.AddDbContext<BusinessOSDbContext>((sp, options) =>
        {
            var tenantProvider = sp.GetRequiredService<ITenantProvider>();
            var tenantDb = sp.GetRequiredService<ITenantDbConnection>();

            Guid tenantId = Guid.Empty;

            if (tenantProvider.HasTenant())
            {
                tenantId = tenantProvider.TenantId;
            }

            var connectionString = tenantDb.GetConnectionString(tenantId);

            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<BusinessOSDbContext>());

        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}

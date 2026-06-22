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
        services.AddDbContext<BusinessOSDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        services.AddScoped<ICurrentUserService, CurrentUserService>();
  
        return services;
    }
}

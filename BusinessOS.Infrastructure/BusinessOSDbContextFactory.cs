using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Infrastructure;

public class BusinessOSDbContextFactory : IDesignTimeDbContextFactory<BusinessOSDbContext>
{
    public BusinessOSDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(basePath, "..", "BusinessOS.API");

        var config = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<BusinessOSDbContext>();

        var connectionString = config.GetConnectionString("DefaultConnection");

        optionsBuilder.UseSqlServer(connectionString);

        // 👇 FIX: provide fake tenant provider
        ITenantProvider tenantProvider = new DesignTimeTenantProvider();

        return new BusinessOSDbContext(optionsBuilder.Options, tenantProvider);
    }
}

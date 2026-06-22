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
        TenantContext.Clear();

        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(basePath, "..", "BusinessOS.API");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(apiPath) ? apiPath : basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        var optionsBuilder = new DbContextOptionsBuilder<BusinessOSDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new BusinessOSDbContext(optionsBuilder.Options);
    }
}

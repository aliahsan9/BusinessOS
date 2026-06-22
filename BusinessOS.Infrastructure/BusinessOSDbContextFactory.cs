using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BusinessOS.Infrastructure;

public class BusinessOSDbContextFactory : IDesignTimeDbContextFactory<BusinessOSDbContext>
{
    public BusinessOSDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var apiPath = File.Exists(Path.Combine(basePath, "appsettings.json"))
            ? basePath
            : Path.GetFullPath(Path.Combine(basePath, "..", "BusinessOS.API"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "DefaultConnection is missing in appsettings.json");

        var optionsBuilder = new DbContextOptionsBuilder<BusinessOSDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new BusinessOSDbContext(optionsBuilder.Options);
    }
}

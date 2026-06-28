using BusinessOS.Application.Common.Authorization;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        var environment = services.GetRequiredService<IHostEnvironment>();
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        var context = services.GetRequiredService<BusinessOSDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILogger<BusinessOSDbContext>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        if (configuration.GetValue<bool>("UseInMemoryDatabase"))
        {
            await context.Database.EnsureCreatedAsync();
        }
        else
        {
            await context.Database.MigrateAsync();
        }

        await RbacSeeder.SeedAsync(context, roleManager, userManager, logger);
    }
}

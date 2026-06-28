using BusinessOS.Application.Common.Authorization;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.IntegrationTests;

public class BusinessOSWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private bool _seeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("UseInMemoryDatabase", "true");
        builder.UseSetting("InMemoryDatabaseName", _databaseName);
    }

    public void EnsureSeeded()
    {
        if (_seeded)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        context.Database.EnsureCreated();

        RbacSeeder.SeedAsync(
                context,
                roleManager,
                userManager,
                NullLogger.Instance)
            .GetAwaiter()
            .GetResult();

        _seeded = true;
    }
}

public abstract class IntegrationTestBase
{
    protected IntegrationTestBase(BusinessOSWebApplicationFactory factory)
    {
        Factory = factory;
        factory.EnsureSeeded();
        Client = factory.CreateClient();
    }

    protected BusinessOSWebApplicationFactory Factory { get; }

    protected HttpClient Client { get; }
}

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<BusinessOSWebApplicationFactory>;

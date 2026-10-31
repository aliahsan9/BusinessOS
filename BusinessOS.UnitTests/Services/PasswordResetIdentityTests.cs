using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BusinessOS.UnitTests.Services;

public class PasswordResetIdentityTests
{
    [Fact]
    public async Task ValidToken_ChangesThePasswordHash_AndCannotBeReused()
    {
        await using var scope = await CreateScopeAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var sessions = scope.ServiceProvider.GetRequiredService<IAuthSessionService>();
        var session = Mock.Get(sessions);

        var created = await users.CreateAsync(User("owner@acme.com"), "OldPassword1");
        created.Succeeded.Should().BeTrue();

        var token = await identity.GeneratePasswordResetTokenAsync("owner@acme.com", CancellationToken.None);
        token.Should().NotBeNullOrWhiteSpace();

        var reset = await identity.ResetPasswordWithTokenAsync(
            "owner@acme.com",
            token!,
            "NewPassword1",
            CancellationToken.None);
        reset.Succeeded.Should().BeTrue();

        var user = await users.FindByEmailAsync("owner@acme.com");
        user!.PasswordHash.Should().NotBeNullOrWhiteSpace();
        user.PasswordHash.Should().NotBe("NewPassword1");
        (await users.CheckPasswordAsync(user, "NewPassword1")).Should().BeTrue();
        (await users.CheckPasswordAsync(user, "OldPassword1")).Should().BeFalse();
        user.SecurityVersion.Should().Be(2);
        session.Verify(x => x.RevokeAllAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);

        var replay = await identity.ResetPasswordWithTokenAsync(
            "owner@acme.com",
            token!,
            "AnotherPass1",
            CancellationToken.None);
        replay.Succeeded.Should().BeFalse();
        (await users.CheckPasswordAsync(user, "NewPassword1")).Should().BeTrue();
    }

    [Fact]
    public async Task InvalidAndExpiredTokens_AreRejected()
    {
        await using var scope = await CreateScopeAsync(TimeSpan.FromMilliseconds(1));
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.CreateAsync(User("owner@acme.com"), "OldPassword1")).Succeeded.Should().BeTrue();

        var invalid = await identity.ResetPasswordWithTokenAsync(
            "owner@acme.com",
            "not-a-real-token",
            "NewPassword1",
            CancellationToken.None);
        invalid.Succeeded.Should().BeFalse();

        var token = await identity.GeneratePasswordResetTokenAsync("owner@acme.com", CancellationToken.None);
        await Task.Delay(50);
        var expired = await identity.ResetPasswordWithTokenAsync(
            "owner@acme.com",
            token!,
            "NewPassword1",
            CancellationToken.None);
        expired.Succeeded.Should().BeFalse();
        var user = await users.FindByEmailAsync("owner@acme.com");
        (await users.CheckPasswordAsync(user!, "OldPassword1")).Should().BeTrue();
    }

    [Fact]
    public async Task WeakPassword_IsRejectedAndStoredHashIsUnchanged()
    {
        await using var scope = await CreateScopeAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.CreateAsync(User("owner@acme.com"), "OldPassword1")).Succeeded.Should().BeTrue();
        var before = (await users.FindByEmailAsync("owner@acme.com"))!.PasswordHash;

        var token = await identity.GeneratePasswordResetTokenAsync("owner@acme.com", CancellationToken.None);
        var result = await identity.ResetPasswordWithTokenAsync(
            "owner@acme.com",
            token!,
            "weak",
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("Password", StringComparison.OrdinalIgnoreCase));
        (await users.FindByEmailAsync("owner@acme.com"))!.PasswordHash.Should().Be(before);
    }

    [Fact]
    public async Task GoogleOnlyAccount_CannotStartALocalPasswordReset()
    {
        await using var scope = await CreateScopeAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var attackerTenant = Guid.NewGuid();
        var ownTenant = Guid.NewGuid();
        scope.ServiceProvider.GetRequiredService<Mock<ICurrentUserService>>()
            .SetupGet(x => x.TenantId)
            .Returns(attackerTenant);

        (await users.CreateAsync(User("google@acme.com"))).Succeeded.Should().BeTrue();
        var user = await users.FindByEmailAsync("google@acme.com");
        (await users.AddLoginAsync(user!, new UserLoginInfo("Google", "sub-google", "Google"))).Succeeded.Should().BeTrue();

        var token = await identity.GeneratePasswordResetTokenAsync("google@acme.com", CancellationToken.None);
        token.Should().BeNull();

        var reset = await identity.ResetPasswordWithTokenAsync(
            "missing@acme.com",
            "anything",
            "NewPassword1",
            CancellationToken.None);
        reset.Succeeded.Should().BeFalse();

        var created = await identity.CreateExternalUserAsync(
            new CreateExternalUserRequest(
                "new@acme.com",
                "Ada",
                "Lovelace",
                ownTenant,
                "Google",
                "sub-new",
                "javascript:alert(1)"),
            CancellationToken.None);
        created.Succeeded.Should().BeTrue();

        var external = await users.FindByEmailAsync("new@acme.com");
        external!.TenantId.Should().Be(ownTenant);
        external.TenantId.Should().NotBe(attackerTenant);
        external.PasswordHash.Should().BeNull();
        external.AvatarUrl.Should().BeNull();
        (await users.GetRolesAsync(external)).Should().BeEmpty();
        (await users.FindByLoginAsync("Google", "sub-new"))!.Id.Should().Be(external.Id);

        var duplicate = await identity.LinkExternalLoginAsync(
            user.Id,
            "Google",
            "sub-new",
            CancellationToken.None);
        duplicate.Succeeded.Should().BeFalse();
    }

    private static ApplicationUser User(string email) => new()
    {
        UserName = email,
        Email = email,
        EmailConfirmed = true,
        FirstName = "Ada",
        LastName = "Lovelace",
        TenantId = Guid.NewGuid(),
        IsActive = true,
        SecurityVersion = 1
    };

    private static async Task<AsyncServiceScope> CreateScopeAsync(TimeSpan? tokenLifetime = null)
    {
        var tenant = new TenantProvider();
        var currentUser = new Mock<ICurrentUserService>();
        var sessions = new Mock<IAuthSessionService>();
        sessions.Setup(x => x.RevokeAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(tenant);
        services.AddDbContext<BusinessOSDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
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
        services.AddDataProtection();
        services.AddLogging();
        services.Configure<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = tokenLifetime ?? TimeSpan.FromHours(24));
        services.AddSingleton(currentUser);
        services.AddSingleton<ICurrentUserService>(currentUser.Object);
        services.AddSingleton(sessions);
        services.AddSingleton(sessions.Object);
        services.AddScoped<IIdentityService, IdentityService>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>().Database.EnsureCreatedAsync();
        return scope;
    }
}

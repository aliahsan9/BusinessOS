using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BusinessOS.API.Middleware;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.MultiTenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.Security;

public class TenantMiddlewareBranchTests
{
    [Fact]
    public async Task AnonymousRequest_IgnoresClientTenantHeader()
    {
        var provider = new TenantProvider();
        var nextCalled = false;
        var context = CreateContext();
        context.Request.Headers["X-Tenant-ID"] = Guid.NewGuid().ToString();

        await InvokeAsync(context, provider, nextCalledFlag: () => nextCalled = true);

        nextCalled.Should().BeTrue();
        provider.HasTenant().Should().BeFalse();
    }

    [Fact]
    public async Task AuthRoute_SkipsTenantResolution()
    {
        var provider = new TenantProvider();
        var nextCalled = false;
        var context = CreateContext(AuthenticatedUser(Guid.NewGuid()));
        context.Request.Path = "/api/v1/auth/login";
        context.Request.Headers["X-Tenant-ID"] = Guid.NewGuid().ToString();

        await InvokeAsync(context, provider, nextCalledFlag: () => nextCalled = true);

        nextCalled.Should().BeTrue();
        provider.HasTenant().Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticatedUser_WithoutNameIdentifier_IsRejected()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Email, "a@test.com")], "Bearer");
        var context = CreateContext(new ClaimsPrincipal(identity));

        var body = await InvokeAsync(context, new TenantProvider());

        body.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        body.Text.Should().Contain("AUTH_REQUIRED");
    }

    [Fact]
    public async Task InactiveMembership_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        var context = CreateContext(AuthenticatedUser(tenantId, "user-1"));
        var lookup = Membership("user-1", tenantId, isActive: false);

        var body = await InvokeAsync(context, new TenantProvider(), lookup);

        body.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        body.Text.Should().Contain("ACCOUNT_INACTIVE");
    }

    [Fact]
    public async Task SpoofedTenantHeader_IsRejected()
    {
        var membershipTenant = Guid.NewGuid();
        var context = CreateContext(AuthenticatedUser(membershipTenant, "user-1"));
        context.Request.Headers["X-Tenant-ID"] = Guid.NewGuid().ToString();

        var body = await InvokeAsync(context, new TenantProvider(), Membership("user-1", membershipTenant, true));

        body.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        body.Text.Should().Contain("TENANT_MISMATCH");
    }

    [Fact]
    public async Task JwtTenant_ThatDoesNotMatchMembership_IsRejected()
    {
        var membershipTenant = Guid.NewGuid();
        var context = CreateContext(AuthenticatedUser(Guid.NewGuid(), "user-1"));

        var body = await InvokeAsync(context, new TenantProvider(), Membership("user-1", membershipTenant, true));

        body.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        body.Text.Should().Contain("TENANT_MISMATCH");
    }

    [Fact]
    public async Task MissingTenantRecord_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        var context = CreateContext(AuthenticatedUser(tenantId, "user-1"));
        var tenantContext = Tenant(isLoaded: false, isActive: false);

        var body = await InvokeAsync(
            context,
            new TenantProvider(),
            Membership("user-1", tenantId, true),
            tenantContext);

        body.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        body.Text.Should().Contain("TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task InactiveTenant_IsRejected()
    {
        var tenantId = Guid.NewGuid();
        var context = CreateContext(AuthenticatedUser(tenantId, "user-1"));

        var body = await InvokeAsync(
            context,
            new TenantProvider(),
            Membership("user-1", tenantId, true),
            Tenant(isLoaded: true, isActive: false));

        body.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        body.Text.Should().Contain("TENANT_INACTIVE");
    }

    [Fact]
    public async Task ValidMembership_UsesDatabaseTenant_NotTheHeader()
    {
        var tenantId = Guid.NewGuid();
        var provider = new TenantProvider();
        var nextCalled = false;
        var context = CreateContext(AuthenticatedUser(tenantId, "user-1"));

        await InvokeAsync(
            context,
            provider,
            Membership("user-1", tenantId, true),
            Tenant(true, true),
            () => nextCalled = true);

        nextCalled.Should().BeTrue();
        provider.TenantId.Should().Be(tenantId);
        TenantContext.CurrentTenantId.Should().Be(Guid.Empty);
    }

    private static ClaimsPrincipal AuthenticatedUser(Guid tenantId, string userId = "user-1") =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("TenantId", tenantId.ToString()),
                new Claim("sec_ver", "1")
            ],
            "Bearer"));

    private static DefaultHttpContext CreateContext(ClaimsPrincipal? user = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/v1/products";
        context.User = user ?? new ClaimsPrincipal(new ClaimsIdentity());
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        return context;
    }

    private static ITenantMembershipLookup Membership(string userId, Guid tenantId, bool isActive)
    {
        var lookup = new Mock<ITenantMembershipLookup>();
        lookup.Setup(x => x.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership(tenantId, isActive));
        return lookup.Object;
    }

    private static ITenantContext Tenant(bool isLoaded, bool isActive)
    {
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(x => x.LoadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tenant.SetupGet(x => x.IsLoaded).Returns(isLoaded);
        tenant.SetupGet(x => x.IsActive).Returns(isActive);
        return tenant.Object;
    }

    private static async Task<(int StatusCode, string Text)> InvokeAsync(
        HttpContext context,
        TenantProvider provider,
        ITenantMembershipLookup? lookup = null,
        ITenantContext? tenantContext = null,
        Action? nextCalledFlag = null)
    {
        lookup ??= Mock.Of<ITenantMembershipLookup>();
        tenantContext ??= Tenant(true, true);

        var middleware = new TenantMiddleware(
            _ =>
            {
                nextCalledFlag?.Invoke();
                return Task.CompletedTask;
            },
            NullLogger<TenantMiddleware>.Instance);

        await middleware.InvokeAsync(context, provider, tenantContext, lookup);

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        if (text.Length > 0)
        {
            using var document = JsonDocument.Parse(text);
            document.RootElement.TryGetProperty("code", out _).Should().BeTrue();
        }

        return (context.Response.StatusCode, text);
    }
}

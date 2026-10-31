using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Activities.Services;
using BusinessOS.Application.Features.Auth;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.MultiTenancy;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace BusinessOS.UnitTests.Services;

public class AuthServicePasswordResetTests
{
    [Fact]
    public async Task ForgotPassword_EligibleAccount_SendsLinkOnConfiguredFrontendOrigin()
    {
        const string token = "abc+def/ghi=";
        var email = new Mock<IEmailNotificationService>();
        EmailMessage? sent = null;
        email.Setup(x => x.SendMessageAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((message, _) => sent = message)
            .ReturnsAsync(new EmailDeliveryResult(true, true));
        var identity = new Mock<IIdentityService>();
        identity.Setup(x => x.GeneratePasswordResetTokenAsync("owner@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        var logger = new CollectingLogger<AuthService>();
        var sut = Create(identity.Object, email.Object, logger, "https://app.example.com");

        var response = await sut.ForgotPasswordAsync("owner@acme.com", CancellationToken.None);

        response.Message.Should().Be(PasswordResetMessages.PublicSuccessMessage);
        sent.Should().NotBeNull();
        sent!.TextBody.Should().Contain("https://app.example.com/auth/reset-password?");
        sent.TextBody.Should().Contain(Uri.EscapeDataString(token));
        sent.TextBody.Should().NotContain(token);
        sent.HtmlBody.Should().Contain("expires in 24 hours");
        sent.HtmlBody.Should().NotContain(token);
        logger.Text.Should().NotContain(token);
    }

    [Fact]
    public async Task ForgotPassword_UnknownAndGoogleOnlyAccounts_ReturnTheSamePublicMessageWithoutEmail()
    {
        var email = new Mock<IEmailNotificationService>();
        var identity = new Mock<IIdentityService>();
        identity.Setup(x => x.GeneratePasswordResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var sut = Create(identity.Object, email.Object, new CollectingLogger<AuthService>(), "https://app.example.com");

        var unknown = await sut.ForgotPasswordAsync("missing@acme.com", CancellationToken.None);
        var googleOnly = await sut.ForgotPasswordAsync("google@acme.com", CancellationToken.None);

        unknown.Message.Should().Be(googleOnly.Message);
        unknown.Message.Should().Be(PasswordResetMessages.PublicSuccessMessage);
        email.Verify(x => x.SendMessageAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_DeliveryFailure_StaysGenericAndDoesNotLogTheToken()
    {
        const string token = "super-secret-reset-token";
        var email = new Mock<IEmailNotificationService>();
        email.Setup(x => x.SendMessageAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp failed token=" + token));
        var identity = new Mock<IIdentityService>();
        identity.Setup(x => x.GeneratePasswordResetTokenAsync("owner@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        var logger = new CollectingLogger<AuthService>();
        var sut = Create(identity.Object, email.Object, logger, "https://app.example.com");

        var response = await sut.ForgotPasswordAsync("owner@acme.com", CancellationToken.None);

        response.Message.Should().Be(PasswordResetMessages.PublicSuccessMessage);
        logger.Text.Should().NotContain(token);
        logger.Text.Should().Contain("token=[redacted]");
    }

    [Fact]
    public async Task ForgotPassword_IsThrottledPerEmailWithoutChangingThePublicMessage()
    {
        var email = new Mock<IEmailNotificationService>();
        email.Setup(x => x.SendMessageAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailDeliveryResult(true, true));
        var identity = new Mock<IIdentityService>();
        identity.Setup(x => x.GeneratePasswordResetTokenAsync("owner@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");
        var sut = Create(identity.Object, email.Object, new CollectingLogger<AuthService>(), "https://app.example.com");

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await sut.ForgotPasswordAsync("owner@acme.com", CancellationToken.None);
            response.Message.Should().Be(PasswordResetMessages.PublicSuccessMessage);
        }

        email.Verify(
            x => x.SendMessageAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Exactly(5));
    }

    [Fact]
    public async Task ResetPassword_HidesTokenFailuresAndReturnsPasswordPolicyErrors()
    {
        var identity = new Mock<IIdentityService>();
        identity.Setup(x => x.ResetPasswordWithTokenAsync("a@b.com", "bad", "NewPassword1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityOperationResult(false, ["Invalid token."]));
        identity.Setup(x => x.ResetPasswordWithTokenAsync("a@b.com", "ok", "weak", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityOperationResult(false, ["Passwords must be at least 8 characters."]));
        var sut = Create(identity.Object, Mock.Of<IEmailNotificationService>(), new CollectingLogger<AuthService>(), "https://app.example.com");

        var invalid = () => sut.ResetPasswordAsync("a@b.com", "bad", "NewPassword1", CancellationToken.None);
        var policy = () => sut.ResetPasswordAsync("a@b.com", "ok", "weak", CancellationToken.None);

        (await invalid.Should().ThrowAsync<BadRequestException>()).Which.Message.Should().Be(PasswordResetMessages.InvalidLinkMessage);
        (await policy.Should().ThrowAsync<BadRequestException>()).Which.Message.Should().Contain("Passwords must be at least 8 characters.");
    }

    [Fact]
    public async Task RegisterExternalUser_CreatesANewTenantAndAssignsOnlyOwner()
    {
        var email = "ada@example.com";
        var tenantProvider = new TenantProvider();
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<BusinessOSDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new TestDbContextFactory(options, tenantProvider);
        Guid createdTenant = Guid.Empty;
        CreateExternalUserRequest? created = null;

        var identity = new Mock<IIdentityService>();
        identity.Setup(x => x.FindByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => createdTenant == Guid.Empty
                ? null
                : new UserAuthResult("user-1", email, createdTenant));
        identity.Setup(x => x.CreateExternalUserAsync(It.IsAny<CreateExternalUserRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateExternalUserRequest, CancellationToken>((request, _) => created = request)
            .ReturnsAsync(new IdentityOperationResult(true, []));
        identity.Setup(x => x.AddToRoleAsync(It.IsAny<UserAuthResult>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenants = new Mock<ITenantRegistrationService>();
        tenants.Setup(x => x.CreateTenantAsync(It.IsAny<CreateTenantOptions>(), It.IsAny<CancellationToken>()))
            .Returns<CreateTenantOptions, CancellationToken>(async (request, cancellationToken) =>
            {
                createdTenant = request.TenantId;
                await using var context = await factory.CreateDbContextAsync(cancellationToken);
                context.Tenants.Add(new Tenant
                {
                    Id = request.TenantId,
                    Name = request.BusinessName,
                    Slug = "analytical",
                    BusinessType = "General",
                    Email = request.Email,
                    Phone = "0",
                    Address = "n/a",
                    OwnerUserId = "pending",
                    SubscriptionPlanId = Guid.NewGuid(),
                    Status = TenantStatus.Active
                });
                await context.SaveChangesAsync(cancellationToken);
                return request.TenantId;
            });

        var roles = new Mock<IRoleRepository>();
        roles.Setup(x => x.GetRoleByNameAsync(RoleNames.Owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);
        roles.Setup(x => x.GetUserRoleNamesAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { RoleNames.Owner });
        roles.Setup(x => x.GetUserPermissionCodesAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        IReadOnlyList<string>? tokenRoles = null;
        Guid tokenTenant = Guid.Empty;
        var jwt = new Mock<IJwtTokenGenerator>();
        jwt.Setup(x => x.GenerateToken(
                "user-1",
                email,
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<int>()))
            .Callback<string, string, Guid, IReadOnlyList<string>, IReadOnlyList<string>, int>((_, _, tenantId, roleNames, _, _) =>
            {
                tokenTenant = tenantId;
                tokenRoles = roleNames;
            })
            .Returns("jwt");
        jwt.Setup(x => x.GetTokenExpiration()).Returns(DateTime.UtcNow.AddMinutes(15));

        var sut = new AuthService(
            identity.Object,
            tenants.Object,
            jwt.Object,
            tenantProvider,
            factory,
            roles.Object,
            Mock.Of<IRbacAuditService>(),
            Mock.Of<IActivityService>(),
            Mock.Of<IEmailNotificationService>(),
            Options.Create(new AppOptions { FrontendBaseUrl = "https://app.example.com" }),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AuthService>.Instance);

        var response = await sut.RegisterExternalUserAsync(
            email,
            "Ada",
            "Lovelace",
            "Analytical Engines",
            "Google",
            "sub-1",
            "https://example.com/a.png",
            CancellationToken.None);

        response.Token.Should().Be("jwt");
        created.Should().NotBeNull();
        created!.TenantId.Should().Be(createdTenant);
        created.TenantId.Should().NotBe(Guid.Empty);
        created.ProviderKey.Should().Be("sub-1");
        tokenTenant.Should().Be(createdTenant);
        tokenRoles.Should().Equal(RoleNames.Owner);
        identity.Verify(x => x.AddToRoleAsync(
            It.IsAny<UserAuthResult>(),
            RoleNames.Owner,
            It.IsAny<CancellationToken>()), Times.Once);
        identity.Verify(x => x.AddToRoleAsync(
            It.IsAny<UserAuthResult>(),
            It.Is<string>(role => role != RoleNames.Owner),
            It.IsAny<CancellationToken>()), Times.Never);
        identity.Verify(x => x.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AuthService Create(
        IIdentityService identity,
        IEmailNotificationService email,
        ILogger<AuthService> logger,
        string frontendBaseUrl) =>
        new(
            identity,
            Mock.Of<ITenantRegistrationService>(),
            Mock.Of<IJwtTokenGenerator>(),
            new TenantProvider(),
            Mock.Of<IDbContextFactory<BusinessOSDbContext>>(),
            Mock.Of<IRoleRepository>(),
            Mock.Of<IRbacAuditService>(),
            Mock.Of<IActivityService>(),
            email,
            Options.Create(new AppOptions
            {
                FrontendBaseUrl = frontendBaseUrl,
                PasswordResetTokenHours = 24
            }),
            new MemoryCache(new MemoryCacheOptions()),
            logger);

    private sealed class TestDbContextFactory : IDbContextFactory<BusinessOSDbContext>
    {
        private readonly DbContextOptions<BusinessOSDbContext> _options;
        private readonly ITenantProvider _tenantProvider;

        public TestDbContextFactory(DbContextOptions<BusinessOSDbContext> options, ITenantProvider tenantProvider)
        {
            _options = options;
            _tenantProvider = tenantProvider;
        }

        public BusinessOSDbContext CreateDbContext() => new(_options, _tenantProvider);

        public Task<BusinessOSDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public string Text { get; private set; } = string.Empty;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Text += formatter(state, exception);
            if (exception is not null)
            {
                Text += exception;
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}

using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Google;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BusinessOS.UnitTests.Services;

public class GoogleAuthServiceTests
{
    private const string RawToken = "google-id-token-must-not-leak";

    [Fact]
    public async Task SignIn_NewVerifiedIdentity_DoesNotCreateUserUntilBusinessNameIsSupplied()
    {
        var identity = GoogleUser();
        var (sut, identityService, auth) = CreateSut(identity);
        identityService.Setup(x => x.FindByLoginAsync("Google", identity.Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuthResult?)null);
        identityService.Setup(x => x.FindByEmailAsync(identity.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuthResult?)null);

        var result = await sut.SignInAsync(RawToken, CancellationToken.None);

        result.Status.Should().Be(GoogleSignInStatus.RegistrationRequired);
        result.ContinuationToken.Should().NotBeNullOrWhiteSpace();
        result.Email.Should().Be(identity.Email);
        result.Auth.Should().BeNull();
        auth.Verify(x => x.RegisterExternalUserAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        identityService.Verify(x => x.AddToRoleAsync(
            It.IsAny<UserAuthResult>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteRegistration_UsesVerifiedSubjectAndDoesNotAcceptAClientTenant()
    {
        var identity = GoogleUser();
        var (sut, identityService, auth) = CreateSut(identity);
        identityService.Setup(x => x.FindByLoginAsync("Google", identity.Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuthResult?)null);
        identityService.Setup(x => x.FindByEmailAsync(identity.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuthResult?)null);
        auth.Setup(x => x.RegisterExternalUserAsync(
                identity.Email,
                "Ada",
                "Lovelace",
                "Analytical Engines",
                "Google",
                identity.Subject,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Session());

        var started = await sut.SignInAsync(RawToken, CancellationToken.None);
        var session = await sut.CompleteRegistrationAsync(
            started.ContinuationToken!,
            "Analytical Engines",
            "Ada",
            "Lovelace",
            CancellationToken.None);

        session.UserId.Should().Be("user-1");
        auth.VerifyAll();
    }

    [Fact]
    public async Task SignIn_ReturningLinkedUser_IssuesSessionWithoutCreatingAnotherUser()
    {
        var identity = GoogleUser();
        var linked = new UserAuthResult("user-1", identity.Email, Guid.NewGuid());
        var (sut, identityService, auth) = CreateSut(identity);
        identityService.Setup(x => x.FindByLoginAsync("Google", identity.Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(linked);
        auth.Setup(x => x.CreateSessionForUserAsync(linked, It.IsAny<CancellationToken>(), "GoogleLogin"))
            .ReturnsAsync(Session());

        var result = await sut.SignInAsync(RawToken, CancellationToken.None);

        result.Status.Should().Be(GoogleSignInStatus.Authenticated);
        result.Auth!.UserId.Should().Be("user-1");
        auth.Verify(x => x.RegisterExternalUserAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(GoogleTokenFailure.Invalid)]
    [InlineData(GoogleTokenFailure.Expired)]
    [InlineData(GoogleTokenFailure.WrongAudience)]
    [InlineData(GoogleTokenFailure.UntrustedIssuer)]
    [InlineData(GoogleTokenFailure.UnverifiedEmail)]
    [InlineData(GoogleTokenFailure.NotConfigured)]
    public async Task SignIn_RejectedToken_IsGenericAndDoesNotEchoTheToken(GoogleTokenFailure failure)
    {
        var validator = new Mock<IGoogleIdTokenValidator>();
        validator.Setup(x => x.ValidateAsync(RawToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleTokenValidationException(failure));
        var sut = CreateSut(validator.Object, new Mock<IIdentityService>(), new Mock<IAuthService>()).Service;

        var act = () => sut.SignInAsync(RawToken, CancellationToken.None);

        var error = await act.Should().ThrowAsync<UnauthorizedException>();
        error.Which.Message.Should().NotContain(RawToken);
        error.Which.Message.Should().NotContain(failure.ToString());
    }

    [Fact]
    public async Task SignIn_ExistingPasswordAccount_IsNotLinkedByEmail()
    {
        var identity = GoogleUser();
        var (sut, identityService, auth) = CreateSut(identity);
        identityService.Setup(x => x.FindByLoginAsync("Google", identity.Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuthResult?)null);
        identityService.Setup(x => x.FindByEmailAsync(identity.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAuthResult("local-user", identity.Email, Guid.NewGuid()));

        var act = () => sut.SignInAsync(RawToken, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        identityService.Verify(x => x.LinkExternalLoginAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        auth.Verify(x => x.CreateSessionForUserAsync(
            It.IsAny<UserAuthResult>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CompleteRegistration_ExpiredTicket_IsRejected()
    {
        var clock = new MutableTimeProvider();
        var identity = GoogleUser();
        var (sut, identityService, _) = CreateSut(identity, clock);
        identityService.Setup(x => x.FindByLoginAsync("Google", identity.Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuthResult?)null);
        identityService.Setup(x => x.FindByEmailAsync(identity.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAuthResult?)null);

        var started = await sut.SignInAsync(RawToken, CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(11);

        var act = () => sut.CompleteRegistrationAsync(
            started.ContinuationToken!,
            "Acme",
            "Ada",
            "Lovelace",
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Link_RejectsGoogleIdentityAlreadyUsedByAnotherUser()
    {
        var identity = GoogleUser();
        var (sut, identityService, _) = CreateSut(identity);
        identityService.Setup(x => x.LinkExternalLoginAsync("user-1", "Google", identity.Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityOperationResult(false, ["This Google account is already linked to another user."]));

        var act = () => sut.LinkAsync("user-1", RawToken, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Unlink_RequiresALocalPassword()
    {
        var (sut, identityService, _) = CreateSut(GoogleUser());
        identityService.Setup(x => x.RemoveExternalLoginAsync("user-1", "Google", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityOperationResult(false, ["Set a password before disconnecting Google so you can still sign in."]));

        var act = () => sut.UnlinkAsync("user-1", CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    private static GoogleIdentity GoogleUser() =>
        new("sub-1", "ada@example.com", true, "Ada", "Lovelace", null);

    private static AuthResponse Session() => new()
    {
        Token = "businessos-jwt",
        UserId = "user-1",
        Email = "ada@example.com",
        TenantId = Guid.NewGuid(),
        ExpiresAt = DateTime.UtcNow.AddMinutes(15)
    };

    private static (GoogleAuthService Service, Mock<IIdentityService> Identity, Mock<IAuthService> Auth) CreateSut(
        GoogleIdentity identity,
        TimeProvider? time = null)
    {
        var validator = new Mock<IGoogleIdTokenValidator>();
        validator.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(identity);
        var identityService = new Mock<IIdentityService>();
        var auth = new Mock<IAuthService>();
        return (CreateSut(validator.Object, identityService, auth, time).Service, identityService, auth);
    }

    private static (GoogleAuthService Service, Mock<IIdentityService> Identity, Mock<IAuthService> Auth) CreateSut(
        IGoogleIdTokenValidator validator,
        Mock<IIdentityService> identity,
        Mock<IAuthService> auth,
        TimeProvider? time = null)
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var sut = new GoogleAuthService(
            validator,
            identity.Object,
            auth.Object,
            provider.GetRequiredService<IDataProtectionProvider>(),
            Mock.Of<IRbacAuditService>(),
            time ?? TimeProvider.System,
            NullLogger<GoogleAuthService>.Instance);
        return (sut, identity, auth);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}

using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Activities.Services;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BusinessOS.UnitTests.Services;

public class AuthServiceRegistrationTests
{
    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ThrowsConflictException()
    {
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(x => x.FindByEmailAsync("a@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAuthResult("user-1", "a@test.com", Guid.NewGuid()));

        var sut = new AuthService(
            identityService.Object,
            Mock.Of<ITenantRegistrationService>(),
            Mock.Of<BusinessOS.Application.Features.Auth.Services.IJwtTokenGenerator>(),
            Mock.Of<ITenantProvider>(),
            Mock.Of<IDbContextFactory<BusinessOS.Infrastructure.Data.BusinessOSDbContext>>(),
            Mock.Of<IRoleRepository>(),
            Mock.Of<IRbacAuditService>(),
            Mock.Of<IActivityService>());

        var act = () => sut.RegisterAsync(
            "a@test.com",
            "Password1!",
            "First",
            "Last",
            "Business",
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ThrowsUnauthorizedException()
    {
        var user = new UserAuthResult("user-1", "a@test.com", Guid.NewGuid());
        var identityService = new Mock<IIdentityService>();
        identityService
            .Setup(x => x.FindByEmailAsync("a@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        identityService
            .Setup(x => x.ValidatePasswordAsync(user, "WrongPass1!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new AuthService(
            identityService.Object,
            Mock.Of<ITenantRegistrationService>(),
            Mock.Of<BusinessOS.Application.Features.Auth.Services.IJwtTokenGenerator>(),
            Mock.Of<ITenantProvider>(),
            Mock.Of<IDbContextFactory<BusinessOS.Infrastructure.Data.BusinessOSDbContext>>(),
            Mock.Of<IRoleRepository>(),
            Mock.Of<IRbacAuditService>(),
            Mock.Of<IActivityService>());

        var act = () => sut.LoginAsync("a@test.com", "WrongPass1!", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}

using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace BusinessOS.UnitTests.Common;

internal static class TestUserManager
{
    public static UserManager<ApplicationUser> Create(ApplicationUser? findById = null)
    {
        var manager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(findById);
        manager.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        manager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        manager.Setup(x => x.GetAuthenticatorKeyAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((string?)null);
        manager.Setup(x => x.SetTwoFactorEnabledAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>()))
            .ReturnsAsync(IdentityResult.Success);
        return manager.Object;
    }
}

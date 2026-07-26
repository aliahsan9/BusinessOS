using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Infrastructure.AI.Copilot;
using FluentAssertions;
using Moq;

namespace BusinessOS.UnitTests.AI;

public class AgentPermissionValidatorTests
{
    [Fact]
    public void ValidateTool_CreateCustomer_RequiresCustomerCreate()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.Roles).Returns([]);
        user.Setup(u => u.HasPermission(PermissionCodes.CustomerCreate)).Returns(false);
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(false);

        var validator = new AiPermissionValidator(user.Object);
        var result = validator.ValidateTool(AiToolName.CreateCustomer);

        result.Allowed.Should().BeFalse();
        result.MissingPermissions.Should().Contain(PermissionCodes.CustomerCreate);
    }

    [Fact]
    public void ValidateTool_CreateCustomer_AllowedWithPermission()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.Roles).Returns(["Employee"]);
        user.Setup(u => u.HasPermission(PermissionCodes.CustomerCreate)).Returns(true);

        var validator = new AiPermissionValidator(user.Object);
        var result = validator.ValidateTool(AiToolName.CreateCustomer);

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateTool_ShowProfit_AllowsManagerRole()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.Roles).Returns(["Manager"]);
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(false);

        var validator = new AiPermissionValidator(user.Object);
        var result = validator.ValidateTool(AiToolName.ShowProfit);

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateTool_UpdateCompanyProfile_RequiresAdmin()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.Roles).Returns(["Cashier"]);
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(false);

        var validator = new AiPermissionValidator(user.Object);
        var result = validator.ValidateTool(AiToolName.UpdateCompanyProfile);

        result.Allowed.Should().BeFalse();
    }

    [Fact]
    public void ValidateTool_AdjustInventory_RequiresInventoryAdjust()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.Roles).Returns([]);
        user.Setup(u => u.HasPermission(PermissionCodes.InventoryAdjust)).Returns(true);

        var validator = new AiPermissionValidator(user.Object);
        validator.ValidateTool(AiToolName.AdjustInventory).Allowed.Should().BeTrue();
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Roles.DTOs;
using BusinessOS.Application.Features.Roles.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Identity;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BusinessOS.UnitTests.Rbac;

public class RoleServiceTests
{
    private readonly Mock<IRoleRepository> _roleRepository = new();
    private readonly Mock<IPermissionRepository> _permissionRepository = new();
    private readonly Mock<IRbacAuditService> _auditService = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<RoleManager<ApplicationRole>> _roleManager;
    private readonly RoleService _sut;

    public RoleServiceTests()
    {
        _userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

        _roleManager = new Mock<RoleManager<ApplicationRole>>(
            Mock.Of<IRoleStore<ApplicationRole>>(), null!, null!, null!, null!);

        _sut = new RoleService(
            _roleRepository.Object,
            _permissionRepository.Object,
            _auditService.Object,
            _userManager.Object,
            _roleManager.Object);
    }

    [Fact]
    public async Task CreateRoleAsync_CreatesRoleAndAudits()
    {
        _roleRepository.Setup(x => x.GetRoleByNameAsync("Auditor", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role?)null);

        _roleRepository.Setup(x => x.CreateRoleAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Role role, CancellationToken _) => role);

        _roleManager.Setup(x => x.RoleExistsAsync("Auditor")).ReturnsAsync(false);
        _roleManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationRole>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.CreateRoleAsync(new CreateRoleRequest("Auditor", "Audit access"));

        result.Name.Should().Be("Auditor");
        _auditService.Verify(x => x.LogAsync(
            "RoleCreated",
            nameof(Role),
            It.IsAny<string>(),
            null,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignPermissionAsync_AssignsPermissionAndAudits()
    {
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        _roleRepository.Setup(x => x.GetRoleByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Role { Id = roleId, Name = "Manager" });

        _permissionRepository.Setup(x => x.GetPermissionByIdAsync(permissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Permission { Id = permissionId, Code = PermissionCodes.ProductView });

        await _sut.AssignPermissionAsync(roleId, permissionId);

        _roleRepository.Verify(x => x.AssignPermissionAsync(roleId, permissionId, It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(x => x.LogAsync(
            "PermissionAssigned",
            nameof(RolePermission),
            $"{roleId}:{permissionId}",
            null,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemovePermissionAsync_RemovesPermissionAndAudits()
    {
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        _roleRepository.Setup(x => x.GetRoleByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Role { Id = roleId, Name = "Manager" });

        _permissionRepository.Setup(x => x.GetPermissionByIdAsync(permissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Permission { Id = permissionId, Code = PermissionCodes.ProductView });

        await _sut.RemovePermissionAsync(roleId, permissionId);

        _roleRepository.Verify(x => x.RemovePermissionAsync(roleId, permissionId, It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(x => x.LogAsync(
            "PermissionRemoved",
            nameof(RolePermission),
            $"{roleId}:{permissionId}",
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignUserRoleAsync_AssignsRoleAndAudits()
    {
        var roleId = Guid.NewGuid();
        const string userId = "user-1";

        _roleRepository.Setup(x => x.GetRoleByIdAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Role { Id = roleId, Name = RoleNames.Sales });

        _userManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(new ApplicationUser { Id = userId, Email = "sales@test.com" });

        _roleManager.Setup(x => x.RoleExistsAsync(RoleNames.Sales)).ReturnsAsync(true);
        _userManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), RoleNames.Sales))
            .ReturnsAsync(IdentityResult.Success);

        await _sut.AssignUserRoleAsync(userId, roleId);

        _roleRepository.Verify(x => x.AssignRoleToUserAsync(userId, roleId, It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(x => x.LogAsync(
            "UserRoleAssigned",
            nameof(UserRole),
            $"{userId}:{roleId}",
            null,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class JwtTokenGeneratorTests
{
    [Fact]
    public void GenerateToken_IncludesRolesAndCompactPermissionsClaim()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-development-key-1234567890",
                ["Jwt:Issuer"] = "BusinessOS",
                ["Jwt:Audience"] = "BusinessOS",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        var generator = new JwtTokenGenerator(configuration);

        var token = generator.GenerateToken(
            "user-1",
            "admin@test.com",
            Guid.NewGuid(),
            [RoleNames.Admin],
            [PermissionCodes.ProductView, PermissionCodes.OrderCreate]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(x => x.Type == ClaimTypes.Role && x.Value == RoleNames.Admin);
        jwt.Claims.Should().Contain(x => x.Type == ClaimTypesConstants.Username && x.Value == "admin");
        jwt.Claims.Should().Contain(x =>
            x.Type == ClaimTypesConstants.Permissions &&
            x.Value == "Order.Create,Product.View");
    }
}

public class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirementAsync_SucceedsWhenPermissionPresent()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(PermissionCodes.ProductCreate);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypesConstants.Permissions, "Category.View,Product.Create")
        ], "Test"));

        var context = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext(
            [requirement],
            user,
            null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_FailsWhenPermissionMissing()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(PermissionCodes.ProductDelete);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypesConstants.Permissions, "Product.View")
        ], "Test"));

        var context = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext(
            [requirement],
            user,
            null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}

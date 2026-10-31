using System.Security.Claims;
using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace BusinessOS.UnitTests.Security;

public class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task MatchingPermission_Succeeds_IgnoringCase()
    {
        var context = await HandleAsync("product.create", PermissionCodes.ProductCreate);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task MissingPermissionClaim_DoesNotSucceed()
    {
        var context = await HandleAsync(null, PermissionCodes.FinanceView);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task UnrelatedPermission_DoesNotGrantAccess()
    {
        var context = await HandleAsync(PermissionCodes.ProductView, PermissionCodes.ProductDelete);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task CommaSeparatedClaim_MatchesOnlyListedCodes()
    {
        var granted = $"{PermissionCodes.ExpenseView}, {PermissionCodes.InvoiceView}";
        var allowed = await HandleAsync(granted, PermissionCodes.InvoiceView);
        var denied = await HandleAsync(granted, PermissionCodes.PaymentCreate);

        allowed.HasSucceeded.Should().BeTrue();
        denied.HasSucceeded.Should().BeFalse();
    }

    private static async Task<AuthorizationHandlerContext> HandleAsync(string? permissions, string required)
    {
        var claims = new List<Claim>();
        if (permissions is not null)
            claims.Add(new Claim(ClaimTypesConstants.Permissions, permissions));

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var requirement = new PermissionRequirement(required);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        await new PermissionAuthorizationHandler().HandleAsync(context);
        return context;
    }
}

public class RolePermissionMatrixTests
{
    [Fact]
    public void EmployeeRole_CannotMutateCatalogFinanceOrOrders()
    {
        PermissionCodes.EmployeePermissions.Should().NotContain(PermissionCodes.ProductCreate);
        PermissionCodes.EmployeePermissions.Should().NotContain(PermissionCodes.FinanceView);
        PermissionCodes.EmployeePermissions.Should().NotContain(PermissionCodes.OrderCreate);
        PermissionCodes.EmployeePermissions.Should().NotContain(PermissionCodes.CustomerDelete);
        PermissionCodes.EmployeePermissions.Should().Contain(PermissionCodes.TaskView);
    }

    [Fact]
    public void AccountantRole_CanReadFinance_AndCannotAdjustInventory()
    {
        PermissionCodes.AccountantPermissions.Should().Contain(PermissionCodes.FinanceView);
        PermissionCodes.AccountantPermissions.Should().Contain(PermissionCodes.ExpenseCreate);
        PermissionCodes.AccountantPermissions.Should().NotContain(PermissionCodes.InventoryAdjust);
        PermissionCodes.AccountantPermissions.Should().NotContain(PermissionCodes.ProductCreate);
    }

    [Fact]
    public void InventoryManagerRole_CanAdjustStock_AndCannotViewFinance()
    {
        PermissionCodes.InventoryManagerPermissions.Should().Contain(PermissionCodes.InventoryAdjust);
        PermissionCodes.InventoryManagerPermissions.Should().Contain(PermissionCodes.PurchaseOrderCreate);
        PermissionCodes.InventoryManagerPermissions.Should().NotContain(PermissionCodes.FinanceView);
    }

    [Fact]
    public void SalesRole_CanSell_AndCannotManageSettings()
    {
        PermissionCodes.SalesPermissions.Should().Contain(PermissionCodes.OrderCreate);
        PermissionCodes.SalesPermissions.Should().Contain(PermissionCodes.CustomerCreate);
        PermissionCodes.SalesPermissions.Should().NotContain(PermissionCodes.SettingsManage);
    }

    [Fact]
    public void AdminPermissions_ExcludeSubscriptionManagement()
    {
        var admin = PermissionCodes.BuildAdminPermissions();

        admin.Should().Contain(PermissionCodes.ProductCreate);
        admin.Should().Contain(PermissionCodes.FinanceView);
        admin.Should().NotContain(PermissionCodes.SubscriptionManage);
    }
}

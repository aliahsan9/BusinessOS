using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;

namespace BusinessOS.Application.Features.DataImports.Services;

public static class DataImportAccess
{
    public static void EnsureCanAccess(string entityType, ICurrentUserService user, bool write)
    {
        var settingsPerm = write ? PermissionCodes.SettingsUpdate : PermissionCodes.SettingsView;
        if (!user.HasPermission(settingsPerm))
            throw new ForbiddenException("You do not have permission to access data import settings.");

        var (view, mutate, update) = ResolvePermissions(entityType);

        if (!user.HasPermission(view))
            throw new ForbiddenException($"You do not have permission to view {entityType}.");

        if (!write)
            return;

        if (!user.HasPermission(mutate))
            throw new ForbiddenException($"You do not have permission to import {entityType}.");

        if (!user.HasPermission(update))
            throw new ForbiddenException($"You do not have permission to update {entityType} during import.");
    }

    private static (string View, string Mutate, string Update) ResolvePermissions(string entityType) =>
        entityType switch
        {
            DataImportEntityTypes.Products =>
                (PermissionCodes.ProductView, PermissionCodes.ProductCreate, PermissionCodes.ProductUpdate),
            DataImportEntityTypes.Customers =>
                (PermissionCodes.CustomerView, PermissionCodes.CustomerCreate, PermissionCodes.CustomerUpdate),
            DataImportEntityTypes.Suppliers =>
                (PermissionCodes.SupplierView, PermissionCodes.SupplierCreate, PermissionCodes.SupplierUpdate),
            DataImportEntityTypes.Categories =>
                (PermissionCodes.CategoryView, PermissionCodes.CategoryCreate, PermissionCodes.CategoryUpdate),
            DataImportEntityTypes.Sales =>
                (PermissionCodes.OrderView, PermissionCodes.OrderCreate, PermissionCodes.OrderUpdate),
            DataImportEntityTypes.Purchases =>
                (PermissionCodes.PurchaseOrderView, PermissionCodes.PurchaseOrderCreate, PermissionCodes.PurchaseOrderUpdate),
            DataImportEntityTypes.Expenses =>
                (PermissionCodes.ExpenseView, PermissionCodes.ExpenseCreate, PermissionCodes.ExpenseUpdate),
            DataImportEntityTypes.CustomerPayments =>
                (PermissionCodes.PaymentView, PermissionCodes.PaymentCreate, PermissionCodes.PaymentUpdate),
            DataImportEntityTypes.SupplierPayments =>
                (PermissionCodes.PurchaseOrderView, PermissionCodes.PurchaseOrderUpdate, PermissionCodes.PurchaseOrderUpdate),
            DataImportEntityTypes.InventoryAdjustments =>
                (PermissionCodes.InventoryView, PermissionCodes.InventoryAdjust, PermissionCodes.InventoryAdjust),
            _ => throw new BadRequestException($"Unsupported entity type '{entityType}'.")
        };
}

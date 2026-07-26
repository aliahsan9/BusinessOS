using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.AI.DTOs;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;

namespace BusinessOS.Infrastructure.AI.Copilot;

public sealed class AiPermissionValidator : IAiPermissionValidator
{
    private readonly ICurrentUserService _currentUser;

    public AiPermissionValidator(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public AiPermissionCheckResult ValidateIntent(AiCopilotIntent intent, IReadOnlyList<AiToolName> tools)
    {
        if (intent is AiCopilotIntent.Conversational or AiCopilotIntent.Help or AiCopilotIntent.DocumentSearch)
            return Allowed();

        if (intent is AiCopilotIntent.Onboarding)
        {
            if (!HasAnyRole("Owner", "Admin"))
            {
                return Denied(
                    "Business onboarding requires Owner or Admin access.",
                    PermissionCodes.SettingsManage);
            }
        }

        if (intent is AiCopilotIntent.Analytics or AiCopilotIntent.DashboardInsight)
        {
            if (!HasAnyRole("Owner", "Admin", "Manager", "Accountant")
                && !_currentUser.HasPermission(PermissionCodes.AnalyticsView)
                && !_currentUser.HasPermission(PermissionCodes.FinanceView))
            {
                return Denied(
                    "Revenue and analytics insights require Manager, Admin, or Owner access.",
                    PermissionCodes.AnalyticsView);
            }
        }

        if (intent is AiCopilotIntent.ReportGeneration)
        {
            if (!_currentUser.HasPermission(PermissionCodes.ReportView)
                && !HasAnyRole("Owner", "Admin", "Manager", "Accountant"))
            {
                return Denied("Report generation requires Report.View permission.", PermissionCodes.ReportView);
            }
        }

        foreach (var tool in tools)
        {
            var toolCheck = ValidateTool(tool);
            if (!toolCheck.Allowed)
                return toolCheck;
        }

        return Allowed();
    }

    public AiPermissionCheckResult ValidateTool(AiToolName tool)
    {
        if (tool is AiToolName.GetRevenue or AiToolName.GetSalesSummary
            or AiToolName.GetBestSellingProducts or AiToolName.GetSalesTrends)
        {
            if (HasAnyRole("Owner", "Admin", "Manager", "Accountant")
                || _currentUser.HasPermission(PermissionCodes.AnalyticsView)
                || _currentUser.HasPermission(PermissionCodes.FinanceView))
            {
                return Allowed();
            }

            return Denied("Revenue analytics require Manager, Admin, or Owner access.", PermissionCodes.AnalyticsView);
        }

        if (tool is AiToolName.ApplyOnboardingProfile or AiToolName.GetBusinessSettings
            or AiToolName.UpdateCompanyProfile or AiToolName.UpdateTaxDefaults)
        {
            if (HasAnyRole("Owner", "Admin"))
                return Allowed();

            return Denied("Business settings and onboarding require Owner or Admin access.", PermissionCodes.SettingsManage);
        }

        if (tool is AiToolName.ShowProfit)
        {
            if (HasAnyRole("Owner", "Admin", "Manager", "Accountant")
                || _currentUser.HasPermission(PermissionCodes.FinanceView)
                || _currentUser.HasPermission(PermissionCodes.AnalyticsView))
            {
                return Allowed();
            }

            return Denied("Profit insights require Finance.View or Manager access.", PermissionCodes.FinanceView);
        }

        if (tool is AiToolName.GetProjects)
        {
            if (_currentUser.HasPermission(PermissionCodes.OrderView)
                || _currentUser.HasPermission(PermissionCodes.ProjectView))
            {
                return Allowed();
            }

            return Denied("You don't have permission to view projects.", PermissionCodes.ProjectView);
        }

        if (tool is AiToolName.CreateProject)
        {
            if (_currentUser.HasPermission(PermissionCodes.OrderCreate)
                || _currentUser.HasPermission(PermissionCodes.ProjectCreate))
            {
                return Allowed();
            }

            return Denied("You don't have permission to create projects.", PermissionCodes.ProjectCreate);
        }

        var required = GetRequiredPermissions(tool);
        if (required.Count == 0)
            return Allowed();

        var missing = required.Where(p => !_currentUser.HasPermission(p)).ToList();
        if (missing.Count == 0)
            return Allowed();

        return Denied($"You don't have permission to use {tool}.", missing.ToArray());
    }

    private static IReadOnlyList<string> GetRequiredPermissions(AiToolName tool) => tool switch
    {
        AiToolName.GetCustomers => [PermissionCodes.CustomerView],
        AiToolName.GetProjects => [],
        AiToolName.GetTasks => [PermissionCodes.TaskView],
        AiToolName.GetInvoices => [PermissionCodes.InvoiceView],
        AiToolName.GetExpenses => [PermissionCodes.ExpenseView],
        AiToolName.GetProducts => [PermissionCodes.ProductView],
        AiToolName.GetRevenue or AiToolName.GetSalesSummary
            or AiToolName.GetBestSellingProducts or AiToolName.GetSalesTrends => [],
        AiToolName.CreateCustomer => [PermissionCodes.CustomerCreate],
        AiToolName.CreateProject => [],
        AiToolName.CreateTask => [PermissionCodes.TaskCreate],
        AiToolName.CreateInvoice => [PermissionCodes.InvoiceCreate],
        AiToolName.SearchDocuments => [],
        AiToolName.GetInventorySummary or AiToolName.GetLowStock or AiToolName.GetDeadStock
            or AiToolName.GetPurchaseRecommendations => [PermissionCodes.InventoryView],
        AiToolName.CreatePurchaseOrderDraft => [PermissionCodes.PurchaseOrderCreate],
        AiToolName.GenerateInventoryReport or AiToolName.GenerateSalesReport => [PermissionCodes.ReportView],
        AiToolName.ApplyOnboardingProfile or AiToolName.GetBusinessSettings => [],
        AiToolName.GetNotificationsSummary => [],
        AiToolName.SearchCustomer => [PermissionCodes.CustomerView],
        AiToolName.UpdateCustomer => [PermissionCodes.CustomerUpdate],
        AiToolName.DeleteCustomer => [PermissionCodes.CustomerDelete],
        AiToolName.SearchProduct => [PermissionCodes.ProductView],
        AiToolName.CreateProduct => [PermissionCodes.ProductCreate],
        AiToolName.UpdateProduct => [PermissionCodes.ProductUpdate],
        AiToolName.DeleteProduct => [PermissionCodes.ProductDelete],
        AiToolName.AdjustInventory or AiToolName.ReceiveStock => [PermissionCodes.InventoryAdjust],
        AiToolName.CreateSale => [PermissionCodes.OrderCreate],
        AiToolName.CancelInvoice => [PermissionCodes.InvoiceUpdate],
        AiToolName.SearchInvoice => [PermissionCodes.InvoiceView],
        AiToolName.CreatePurchaseOrder => [PermissionCodes.PurchaseOrderCreate],
        AiToolName.ApprovePurchaseOrder or AiToolName.ReceivePurchase => [PermissionCodes.PurchaseOrderUpdate],
        AiToolName.SearchSupplier => [PermissionCodes.SupplierView],
        AiToolName.CreateSupplier => [PermissionCodes.SupplierCreate],
        AiToolName.UpdateSupplier => [PermissionCodes.SupplierUpdate],
        AiToolName.DeleteSupplier => [PermissionCodes.SupplierDelete],
        AiToolName.ShowProfit => [PermissionCodes.FinanceView],
        AiToolName.UpdateCompanyProfile or AiToolName.UpdateTaxDefaults => [PermissionCodes.SettingsManage],
        _ => []
    };

    private bool HasAnyRole(params string[] roles) =>
        _currentUser.Roles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase));

    private static AiPermissionCheckResult Allowed() => new() { Allowed = true };

    private static AiPermissionCheckResult Denied(string reason, params string[] missing) =>
        new()
        {
            Allowed = false,
            DenialReason = reason,
            MissingPermissions = missing
        };
}

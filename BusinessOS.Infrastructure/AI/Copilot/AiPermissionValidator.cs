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
        if (tool is AiToolName.GetRevenue or AiToolName.GetSalesSummary)
        {
            if (HasAnyRole("Owner", "Admin", "Manager", "Accountant")
                || _currentUser.HasPermission(PermissionCodes.AnalyticsView)
                || _currentUser.HasPermission(PermissionCodes.FinanceView))
            {
                return Allowed();
            }

            return Denied("Revenue analytics require Manager, Admin, or Owner access.", PermissionCodes.AnalyticsView);
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
        AiToolName.GetRevenue or AiToolName.GetSalesSummary => [],
        AiToolName.CreateCustomer => [PermissionCodes.CustomerCreate],
        AiToolName.CreateProject => [],
        AiToolName.CreateTask => [PermissionCodes.TaskCreate],
        AiToolName.CreateInvoice => [PermissionCodes.InvoiceCreate],
        AiToolName.SearchDocuments => [],
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

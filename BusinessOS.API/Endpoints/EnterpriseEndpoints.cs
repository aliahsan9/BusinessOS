using BusinessOS.API.Authorization;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Enterprise;

namespace BusinessOS.API.Endpoints;

public static class EnterpriseEndpoints
{
    public static void MapEnterpriseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/enterprise")
            .WithTags("Enterprise")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/hierarchy", async (IEnterpriseOrganizationService service, CancellationToken ct) =>
            Results.Ok(await service.GetHierarchyAsync(ct)))
            .RequirePermission(PermissionCodes.EnterpriseView);

        group.MapGet("/branches", async (IEnterpriseOrganizationService service, CancellationToken ct) =>
            Results.Ok(await service.GetBranchesAsync(ct)))
            .RequirePermission(PermissionCodes.OrgBranchView);

        group.MapPost("/branches", async (CompanyBranchWriteRequest request, IEnterpriseOrganizationService service, CancellationToken ct) =>
            Results.Ok(await service.SaveBranchAsync(null, request, ct)))
            .RequirePermission(PermissionCodes.OrgBranchManage);

        group.MapPut("/branches/{id:guid}", async (Guid id, CompanyBranchWriteRequest request, IEnterpriseOrganizationService service, CancellationToken ct) =>
            Results.Ok(await service.SaveBranchAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.OrgBranchManage);

        group.MapGet("/permission-groups", async (IEnterpriseRbacService service, CancellationToken ct) =>
            Results.Ok(await service.GetPermissionGroupsAsync(ct)))
            .RequirePermission(PermissionCodes.RoleView);

        group.MapPost("/permission-groups", async (PermissionGroupWriteRequest request, IEnterpriseRbacService service, CancellationToken ct) =>
            Results.Ok(await service.SavePermissionGroupAsync(null, request, ct)))
            .RequirePermission(PermissionCodes.RoleUpdate);

        group.MapPut("/permission-groups/{id:guid}", async (Guid id, PermissionGroupWriteRequest request, IEnterpriseRbacService service, CancellationToken ct) =>
            Results.Ok(await service.SavePermissionGroupAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.RoleUpdate);

        group.MapGet("/temporary-permissions", async (string? userId, IEnterpriseRbacService service, CancellationToken ct) =>
            Results.Ok(await service.GetTemporaryGrantsAsync(userId, ct)))
            .RequirePermission(PermissionCodes.RoleView);

        group.MapPost("/temporary-permissions", async (TemporaryPermissionWriteRequest request, IEnterpriseRbacService service, CancellationToken ct) =>
            Results.Ok(await service.GrantTemporaryAsync(request, ct)))
            .RequirePermission(PermissionCodes.RoleUpdate);

        group.MapPost("/temporary-permissions/{id:guid}/revoke", async (Guid id, IEnterpriseRbacService service, CancellationToken ct) =>
        {
            await service.RevokeTemporaryAsync(id, ct);
            return Results.NoContent();
        }).RequirePermission(PermissionCodes.RoleUpdate);

        group.MapGet("/role-assignment-history", async (string? userId, IEnterpriseRbacService service, CancellationToken ct) =>
            Results.Ok(await service.GetAssignmentHistoryAsync(userId, ct)))
            .RequirePermission(PermissionCodes.RoleView);

        group.MapGet("/approval-policies", async (IApprovalGovernanceService service, CancellationToken ct) =>
            Results.Ok(await service.GetPoliciesAsync(ct)))
            .RequirePermission(PermissionCodes.ApprovalPolicyView);

        group.MapPost("/approval-policies", async (ApprovalPolicyWriteRequest request, IApprovalGovernanceService service, CancellationToken ct) =>
            Results.Ok(await service.SavePolicyAsync(null, request, ct)))
            .RequirePermission(PermissionCodes.ApprovalPolicyManage);

        group.MapPut("/approval-policies/{id:guid}", async (Guid id, ApprovalPolicyWriteRequest request, IApprovalGovernanceService service, CancellationToken ct) =>
            Results.Ok(await service.SavePolicyAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.ApprovalPolicyManage);

        group.MapGet("/approval-requests", async (string? status, IApprovalGovernanceService service, CancellationToken ct) =>
            Results.Ok(await service.GetRequestsAsync(status, ct)))
            .RequirePermission(PermissionCodes.ApprovalPolicyView);

        group.MapPost("/approval-requests/{id:guid}/decide", async (Guid id, ApprovalDecisionWriteRequest request, IApprovalGovernanceService service, CancellationToken ct) =>
            Results.Ok(await service.DecideAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.ApprovalDecide);

        group.MapPost("/approval-requests/{id:guid}/delegate", async (Guid id, ApprovalDelegateRequest request, IApprovalGovernanceService service, CancellationToken ct) =>
            Results.Ok(await service.DelegateAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.ApprovalDecide);

        group.MapGet("/audit", async (string? category, string? actorUserId, DateTime? from, DateTime? to, string? search, int? page, int? pageSize, IEnterpriseAuditService service, CancellationToken ct) =>
            Results.Ok(await service.QueryAsync(new EnterpriseAuditQuery
            {
                Category = category,
                ActorUserId = actorUserId,
                From = from,
                To = to,
                Search = search,
                Page = page ?? 1,
                PageSize = pageSize ?? 50
            }, ct)))
            .RequirePermission(PermissionCodes.AuditView);

        group.MapGet("/audit/export", async (string? category, string? actorUserId, DateTime? from, DateTime? to, string? search, IEnterpriseAuditService service, CancellationToken ct) =>
        {
            var bytes = await service.ExportCsvAsync(new EnterpriseAuditQuery
            {
                Category = category,
                ActorUserId = actorUserId,
                From = from,
                To = to,
                Search = search
            }, ct);
            return Results.File(bytes, "text/csv", "enterprise-audit.csv");
        }).RequirePermission(PermissionCodes.AuditExport);

        group.MapGet("/reports/branches", async (Guid? branchId, DateTime? from, DateTime? to, IEnterpriseReportingService service, CancellationToken ct) =>
            Results.Ok(await service.GetBranchReportAsync(branchId, from, to, ct)))
            .RequirePermission(PermissionCodes.ReportView);

        group.MapGet("/reports/departments", async (Guid? departmentId, DateTime? from, DateTime? to, IEnterpriseReportingService service, CancellationToken ct) =>
            Results.Ok(await service.GetDepartmentReportAsync(departmentId, from, to, ct)))
            .RequirePermission(PermissionCodes.ReportView);

        group.MapGet("/reports/approvals", async (DateTime? from, DateTime? to, IEnterpriseReportingService service, CancellationToken ct) =>
            Results.Ok(await service.GetApprovalAnalyticsAsync(from, to, ct)))
            .RequirePermission(PermissionCodes.ReportView);

        group.MapGet("/reports/agents", async (DateTime? from, DateTime? to, IEnterpriseReportingService service, CancellationToken ct) =>
            Results.Ok(await service.GetAgentProductivityAsync(from, to, ct)))
            .RequirePermission(PermissionCodes.ReportView);

        group.MapGet("/reports/finance", async (DateTime? from, DateTime? to, IEnterpriseReportingService service, CancellationToken ct) =>
            Results.Ok(await service.GetConsolidatedFinanceAsync(from, to, ct)))
            .RequirePermission(PermissionCodes.FinanceView);

        group.MapGet("/sso/configurations", async (IEnterpriseSsoService service, CancellationToken ct) =>
            Results.Ok(await service.GetConfigurationsAsync(ct)))
            .RequirePermission(PermissionCodes.SsoManage);

        group.MapPost("/sso/configurations", async (TenantSsoWriteRequest request, IEnterpriseSsoService service, CancellationToken ct) =>
            Results.Ok(await service.SaveConfigurationAsync(null, request, ct)))
            .RequirePermission(PermissionCodes.SsoManage);

        group.MapPut("/sso/configurations/{id:guid}", async (Guid id, TenantSsoWriteRequest request, IEnterpriseSsoService service, CancellationToken ct) =>
            Results.Ok(await service.SaveConfigurationAsync(id, request, ct)))
            .RequirePermission(PermissionCodes.SsoManage);

        group.MapGet("/security-settings", async (IEnterpriseSsoService service, CancellationToken ct) =>
            Results.Ok(await service.GetSecuritySettingsAsync(ct)))
            .RequirePermission(PermissionCodes.SsoManage);

        group.MapPut("/security-settings", async (EnterpriseSecuritySettingsWriteRequest request, IEnterpriseSsoService service, CancellationToken ct) =>
            Results.Ok(await service.SaveSecuritySettingsAsync(request, ct)))
            .RequirePermission(PermissionCodes.SsoManage);

        group.MapGet("/agents/{key}/governance", async (string key, IEnterpriseAgentGovernanceService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(key, ct)))
            .RequirePermission(PermissionCodes.AgentGovernanceManage);

        group.MapPut("/agents/{key}/governance", async (string key, AgentGovernanceWriteRequest request, IEnterpriseAgentGovernanceService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAsync(key, request, ct)))
            .RequirePermission(PermissionCodes.AgentGovernanceManage);

        group.MapPost("/mfa/setup", async (IEnterpriseMfaService service, CancellationToken ct) =>
            Results.Ok(await service.BeginSetupAsync(ct)));

        group.MapPost("/mfa/enable", async (MfaCodeRequest request, IEnterpriseMfaService service, CancellationToken ct) =>
        {
            await service.EnableAsync(request.Code, ct);
            return Results.NoContent();
        });

        group.MapPost("/mfa/disable", async (MfaCodeRequest request, IEnterpriseMfaService service, CancellationToken ct) =>
        {
            await service.DisableAsync(request.Code, ct);
            return Results.NoContent();
        });

        // Public SSO challenge/callback endpoints (real IdP redirects; not simulated).
        var publicSso = app.MapGroup("/enterprise/sso")
            .WithTags("Enterprise SSO")
            .RequireRateLimiting("auth");

        publicSso.MapGet("/oidc/{id:guid}/begin", async (Guid id, string? returnUrl, IEnterpriseSsoService service, CancellationToken ct) =>
            Results.Ok(await service.BeginOidcAsync(id, returnUrl ?? "/", ct)));

        publicSso.MapGet("/oidc/callback", async (string code, string state, IEnterpriseSsoService service, CancellationToken ct) =>
            Results.Ok(await service.CompleteOidcAsync(new SsoOidcCallbackRequest
            {
                Code = code,
                State = state
            }, ct)));

        publicSso.MapPost("/oidc/callback", async (SsoOidcCallbackRequest request, IEnterpriseSsoService service, CancellationToken ct) =>
            Results.Ok(await service.CompleteOidcAsync(request, ct)));

        publicSso.MapGet("/saml/{id:guid}/begin", async (Guid id, string? returnUrl, IEnterpriseSsoService service, CancellationToken ct) =>
            Results.Ok(await service.BeginSamlAsync(id, returnUrl ?? "/", ct)));

        publicSso.MapPost("/saml/{tenantId:guid}/acs", async (Guid tenantId, HttpRequest http, IEnterpriseSsoService service, ITenantProvider tenant, CancellationToken ct) =>
        {
            tenant.SetTenantId(tenantId);
            var form = await http.ReadFormAsync(ct);
            var samlResponse = form["SAMLResponse"].ToString();
            var relayState = form["RelayState"].ToString();
            return Results.Ok(await service.CompleteSamlAsync(new SsoSamlCallbackRequest
            {
                ConfigurationId = Guid.Empty,
                SamlResponse = samlResponse,
                RelayState = relayState
            }, ct));
        });

        var authMfa = app.MapGroup("/auth/mfa")
            .WithTags("Authentication")
            .RequireRateLimiting("auth");

        authMfa.MapPost("/verify", async (MfaVerifyRequest request, IEnterpriseMfaService service, CancellationToken ct) =>
            Results.Ok(await service.VerifyLoginAsync(request.MfaToken, request.Code, ct)))
            .Produces<AuthResponse>();
    }

    private sealed class MfaCodeRequest
    {
        public string Code { get; set; } = default!;
    }

    private sealed class MfaVerifyRequest
    {
        public string MfaToken { get; set; } = default!;
        public string Code { get; set; } = default!;
    }
}

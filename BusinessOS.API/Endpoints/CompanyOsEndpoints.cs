using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Features.Platform;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class CompanyOsEndpoints
{
    public static void MapCompanyOsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/business-types", async (IBusinessTypeCatalogService catalog, CancellationToken cancellationToken) =>
                Results.Ok(await catalog.ListAsync(cancellationToken)))
            .WithTags("Company")
            .AllowAnonymous()
            .WithName("ListBusinessTypes");

        var companies = app.MapGroup("/companies")
            .WithTags("Company")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        companies.MapGet("/mine", async (ICompanyMembershipService memberships, CancellationToken cancellationToken) =>
                Results.Ok(await memberships.ListMineAsync(cancellationToken)))
            .WithName("ListMyCompanies");

        companies.MapPost("/switch", async (SwitchCompanyRequest request, ICompanyMembershipService memberships, CancellationToken cancellationToken) =>
                Results.Ok(await memberships.SwitchAsync(request.CompanyId, cancellationToken)))
            .WithName("SwitchCompany")
            .Produces<AuthResponse>();

        var tenant = app.MapGroup("/tenant")
            .WithTags("Company")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        tenant.MapPost("/transfer-ownership", async (TransferOwnershipRequest request, ICompanyMembershipService memberships, CancellationToken cancellationToken) =>
            {
                await memberships.TransferOwnershipAsync(request.NewOwnerUserId, cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(PermissionCodes.TenantManage)
            .WithName("TransferCompanyOwnership");

        tenant.MapGet("/announcements", async (ICompanyAnnouncementService announcements, CancellationToken cancellationToken) =>
                Results.Ok(await announcements.ListVisibleAsync(cancellationToken)))
            .RequirePermission(PermissionCodes.TenantView)
            .WithName("ListCompanyAnnouncements");

        tenant.MapGet("/resources", async (ICompanyKnowledgeService knowledge, CancellationToken cancellationToken) =>
                Results.Ok(await knowledge.GetResourcesAsync(cancellationToken)))
            .RequirePermission(PermissionCodes.TenantView)
            .WithName("GetCompanyResources");

        tenant.MapGet("/knowledge", async (ICompanyKnowledgeService knowledge, CancellationToken cancellationToken) =>
                Results.Ok(await knowledge.ListAsync(cancellationToken)))
            .RequirePermission(PermissionCodes.TenantView)
            .WithName("ListCompanyKnowledge");

        tenant.MapDelete("/knowledge/{documentId:guid}", async (Guid documentId, ICompanyKnowledgeService knowledge, CancellationToken cancellationToken) =>
            {
                await knowledge.DeleteAsync(documentId, cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(PermissionCodes.TenantManage)
            .WithName("DeleteCompanyKnowledge");

        tenant.MapGet("/files", async (ICompanyFileStore files, CancellationToken cancellationToken) =>
                Results.Ok(await files.ListAsync(cancellationToken)))
            .RequirePermission(PermissionCodes.TenantView)
            .WithName("ListCompanyFiles");

        tenant.MapPost("/files", async (IFormFile file, ICompanyFileStore files, CancellationToken cancellationToken) =>
            {
                await using var stream = file.OpenReadStream();
                var saved = await files.SaveAsync(stream, file.FileName, file.ContentType, cancellationToken);
                return Results.Created($"/api/v1/tenant/files/{saved.Id}", saved);
            })
            .RequirePermission(PermissionCodes.TenantManage)
            .DisableAntiforgery()
            .WithName("UploadCompanyFile");

        tenant.MapGet("/files/{fileId:guid}", async (Guid fileId, ICompanyFileStore files, CancellationToken cancellationToken) =>
            {
                var opened = await files.OpenAsync(fileId, cancellationToken);
                return Results.File(opened.Content, opened.File.ContentType, opened.File.FileName);
            })
            .RequirePermission(PermissionCodes.TenantView)
            .WithName("DownloadCompanyFile");

        tenant.MapDelete("/files/{fileId:guid}", async (Guid fileId, ICompanyFileStore files, CancellationToken cancellationToken) =>
            {
                await files.DeleteAsync(fileId, cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(PermissionCodes.TenantManage)
            .WithName("DeleteCompanyFile");

        var platform = app.MapGroup("/platform")
            .WithTags("Platform Admin")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        platform.MapGet("/session", (IPlatformAdminService admin) => Results.Ok(admin.GetSession()))
            .WithName("GetPlatformSession");

        platform.MapGet("/companies", async (string? search, string? status, int? page, int? pageSize, IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.ListCompaniesAsync(search, status, page ?? 1, pageSize ?? 25, cancellationToken)))
            .WithName("ListPlatformCompanies")
            .Produces<PagedResult<CompanySummaryDto>>();

        platform.MapGet("/companies/{companyId:guid}", async (Guid companyId, IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.GetCompanyAsync(companyId, cancellationToken)))
            .WithName("GetPlatformCompany");

        platform.MapPost("/companies/{companyId:guid}/status", async (Guid companyId, SetCompanyStatusRequest request, IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.SetCompanyStatusAsync(companyId, request.Status, cancellationToken)))
            .WithName("SetPlatformCompanyStatus");

        platform.MapGet("/subscriptions", async (IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.ListSubscriptionsAsync(cancellationToken)))
            .WithName("ListPlatformSubscriptions");

        platform.MapGet("/revenue", async (IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.GetRevenueAsync(cancellationToken)))
            .WithName("GetPlatformRevenue");

        platform.MapGet("/ai-usage", async (Guid? companyId, string? operation, DateTime? from, DateTime? to, IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.GetAiUsageAsync(companyId, operation, from, to, cancellationToken)))
            .WithName("GetPlatformAiUsage");

        platform.MapGet("/agents", async (Guid? companyId, string? agent, string? status, int? page, int? pageSize, IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.ListAgentExecutionsAsync(companyId, agent, status, page ?? 1, pageSize ?? 25, cancellationToken)))
            .WithName("ListPlatformAgentExecutions");

        platform.MapGet("/agents/{executionId:guid}", async (Guid executionId, IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.GetAgentExecutionAsync(executionId, cancellationToken)))
            .WithName("GetPlatformAgentExecution");

        platform.MapGet("/health", async (IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.GetHealthAsync(cancellationToken)))
            .WithName("GetPlatformHealth");

        platform.MapGet("/ai-settings", async (IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.GetAiSettingsAsync(cancellationToken)))
            .WithName("GetPlatformAiSettings");

        platform.MapPut("/ai-settings", async (UpsertPlatformSettingRequest request, IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.UpsertAiSettingAsync(request.Key, request.Value, cancellationToken)))
            .WithName("UpsertPlatformAiSetting");

        platform.MapGet("/announcements", async (IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.ListAnnouncementsAsync(cancellationToken)))
            .WithName("ListPlatformAnnouncements");

        platform.MapPost("/announcements", async (SaveAnnouncementRequest request, IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.SaveAnnouncementAsync(null, request, cancellationToken)))
            .WithName("CreatePlatformAnnouncement");

        platform.MapPut("/announcements/{id:guid}", async (Guid id, SaveAnnouncementRequest request, IPlatformAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.SaveAnnouncementAsync(id, request, cancellationToken)))
            .WithName("UpdatePlatformAnnouncement");

        platform.MapPost("/announcements/{id:guid}/deactivate", async (Guid id, IPlatformAdminService admin, CancellationToken cancellationToken) =>
            {
                await admin.DeactivateAnnouncementAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeactivatePlatformAnnouncement");

        platform.MapPut("/plans/{planId:guid}", async (Guid planId, CommercialPlanUpdateRequest request, ICommercialAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.UpdatePlanAsync(planId, request, cancellationToken)))
            .WithName("UpdateCommercialPlan");

        platform.MapPut("/plans/{planId:guid}/features/{featureKey}", async (Guid planId, string featureKey, FeatureGrantRequest request, ICommercialAdminService admin, CancellationToken cancellationToken) =>
            {
                await admin.UpsertPlanFeatureAsync(planId, featureKey, request, cancellationToken);
                return Results.NoContent();
            })
            .WithName("UpsertPlanFeature");

        platform.MapPut("/companies/{companyId:guid}/features/{featureKey}", async (Guid companyId, string featureKey, FeatureGrantRequest request, ICommercialAdminService admin, CancellationToken cancellationToken) =>
            {
                await admin.UpsertCompanyFeatureAsync(companyId, featureKey, request, cancellationToken);
                return Results.NoContent();
            })
            .WithName("UpsertCompanyFeature");

        platform.MapPost("/companies/{companyId:guid}/trial", async (Guid companyId, GrantTrialRequest request, ICommercialAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.GrantTrialAsync(companyId, request, cancellationToken)))
            .WithName("GrantCompanyTrial");

        platform.MapPost("/companies/{companyId:guid}/subscription/suspend", async (Guid companyId, ICommercialAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.SuspendSubscriptionAsync(companyId, cancellationToken)))
            .WithName("SuspendCompanySubscription");

        platform.MapPost("/companies/{companyId:guid}/subscription/reactivate", async (Guid companyId, ICommercialAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.ReactivateSubscriptionAsync(companyId, cancellationToken)))
            .WithName("ReactivateCompanySubscription");

        platform.MapGet("/payments/failed", async (ICommercialAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.ListFailedPaymentsAsync(cancellationToken)))
            .WithName("ListFailedSubscriptionPayments");

        platform.MapGet("/model-prices", async (ICommercialAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.ListModelPricesAsync(cancellationToken)))
            .WithName("ListAiModelPrices");

        platform.MapPut("/model-prices", async (ModelPriceRequest request, ICommercialAdminService admin, CancellationToken cancellationToken) =>
                Results.Ok(await admin.UpsertModelPriceAsync(request, cancellationToken)))
            .WithName("UpsertAiModelPrice");
    }
}

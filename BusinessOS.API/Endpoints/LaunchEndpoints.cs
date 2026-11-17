using BusinessOS.Application.Features.Launch;

namespace BusinessOS.API.Endpoints;

public static class LaunchEndpoints
{
    public static void MapLaunchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/launch/notice", (IAgencyLaunchService launch) => Results.Ok(launch.GetNotice()))
            .WithTags("Launch")
            .AllowAnonymous()
            .WithName("GetLaunchNotice");

        var group = app.MapGroup("/launch")
            .WithTags("Launch")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/setup", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.GetSetupAsync(cancellationToken)))
            .WithName("GetAgencySetup");

        group.MapPut("/company", async (AgencyCompanyWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.SaveCompanyAsync(request, cancellationToken)))
            .WithName("SaveAgencyCompany");

        group.MapPut("/profile", async (AgencyProfileWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.SaveProfileAsync(request, cancellationToken)))
            .WithName("SaveAgencyProfile");

        group.MapPut("/workspace", async (AgencyWorkspaceWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.SaveWorkspaceAsync(request, cancellationToken)))
            .WithName("SaveAgencyWorkspace");

        group.MapPut("/workforce", async (AgencyWorkforceWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.SaveWorkforceAsync(request, cancellationToken)))
            .WithName("SaveAgencyWorkforce");

        group.MapPut("/plan", async (AgencyPlanWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.SavePlanAsync(request, cancellationToken)))
            .WithName("SaveAgencyPlan");

        group.MapPut("/objective", async (AgencyObjectiveWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.SaveObjectiveAsync(request, cancellationToken)))
            .WithName("SaveAgencyObjective");

        group.MapPost("/invitations/redeem", async (RedeemInvitationWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.RedeemInvitationAsync(request.Code, cancellationToken)))
            .WithName("RedeemEarlyAccessInvitation");

        group.MapPost("/feedback", async (LaunchFeedbackWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.SubmitFeedbackAsync(request, cancellationToken)))
            .WithName("SubmitLaunchFeedback");

        group.MapGet("/feedback", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.ListFeedbackAsync(cancellationToken)))
            .WithName("ListLaunchFeedback");

        group.MapPost("/usage", async (UsageEventWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
            {
                await launch.RecordUsageAsync(request.EventKey, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RecordLaunchUsage");

        group.MapGet("/analytics", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.GetCompanyAnalyticsAsync(cancellationToken)))
            .WithName("GetCompanyLaunchAnalytics");

        group.MapPost("/data/export", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.ExportAsync(cancellationToken)))
            .WithName("ExportCompanyData");

        group.MapGet("/data/export", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.LatestExportAsync(cancellationToken)))
            .WithName("GetLatestCompanyExport");

        group.MapPost("/data/delete", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.RequestDeletionAsync(cancellationToken)))
            .WithName("RequestCompanyDeletion");

        var platform = app.MapGroup("/platform/launch")
            .WithTags("Launch")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        platform.MapPost("/invitations", async (CreateInvitationWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.CreateInvitationAsync(request, cancellationToken)))
            .WithName("CreateEarlyAccessInvitation");

        platform.MapGet("/invitations", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.ListInvitationsAsync(cancellationToken)))
            .WithName("ListEarlyAccessInvitations");

        platform.MapPost("/invitations/{id:guid}/revoke", async (Guid id, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
            {
                await launch.RevokeInvitationAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RevokeEarlyAccessInvitation");

        platform.MapPut("/invitation-requirement", async (InvitationRequirementWrite request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
            {
                await launch.SetInvitationRequiredAsync(request.Required, cancellationToken);
                return Results.NoContent();
            })
            .WithName("SetEarlyAccessInvitationRequirement");

        platform.MapGet("/analytics", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.GetPlatformAnalyticsAsync(cancellationToken)))
            .WithName("GetPlatformLaunchAnalytics");

        platform.MapGet("/feedback", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.ListPlatformFeedbackAsync(cancellationToken)))
            .WithName("ListPlatformLaunchFeedback");

        platform.MapGet("/backlog", async (IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.ListBacklogAsync(cancellationToken)))
            .WithName("ListProductBacklog");

        platform.MapPut("/backlog/{id:guid}", async (Guid id, BacklogUpdate request, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.UpdateBacklogAsync(id, request, cancellationToken)))
            .WithName("UpdateProductBacklog");

        platform.MapPost("/companies/{companyId:guid}/deletion/confirm", async (Guid companyId, IAgencyLaunchService launch, CancellationToken cancellationToken) =>
                Results.Ok(await launch.ConfirmDeletionAsync(companyId, cancellationToken)))
            .WithName("ConfirmCompanyDeletion");
    }
}

public sealed class RedeemInvitationWrite
{
    public string Code { get; set; } = "";
}

public sealed class UsageEventWrite
{
    public string EventKey { get; set; } = "";
}

public sealed class InvitationRequirementWrite
{
    public bool Required { get; set; }
}

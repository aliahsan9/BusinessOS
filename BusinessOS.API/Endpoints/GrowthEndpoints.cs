using BusinessOS.Application.Features.Growth;

namespace BusinessOS.API.Endpoints;

public static class GrowthEndpoints
{
    public static void MapGrowthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/growth/leads", async (MarketingLeadWrite request, IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Created($"/api/v1/platform/growth/leads", await growth.SubmitLeadAsync(request, cancellationToken)))
            .WithTags("Growth")
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithName("SubmitMarketingLead");

        app.MapGet("/growth/referrals/validate", async (string code, IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Ok(await growth.ValidateReferralCodeAsync(code, cancellationToken)))
            .WithTags("Growth")
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithName("ValidateReferralCode");

        app.MapPost("/growth/support", async (PlatformSupportTicketWrite request, IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Created($"/api/v1/growth/support", await growth.CreateSupportTicketAsync(request, cancellationToken)))
            .WithTags("Growth")
            .AllowAnonymous()
            .RequireRateLimiting("public")
            .WithName("CreatePlatformSupportTicketPublic");

        var auth = app.MapGroup("/growth")
            .WithTags("Growth")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        auth.MapGet("/support", async (IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Ok(await growth.ListMySupportTicketsAsync(cancellationToken)))
            .WithName("ListMyPlatformSupportTickets");

        auth.MapPost("/support/authenticated", async (PlatformSupportTicketWrite request, IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Created($"/api/v1/growth/support", await growth.CreateSupportTicketAsync(request, cancellationToken)))
            .WithName("CreatePlatformSupportTicketAuthenticated");

        auth.MapGet("/referrals/me", async (IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Ok(await growth.GetOrCreateReferralCodeAsync(cancellationToken)))
            .WithName("GetMyReferralCode");

        var platform = app.MapGroup("/platform/growth")
            .WithTags("Growth")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        platform.MapGet("/leads", async (string? status, string? kind, IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Ok(await growth.ListLeadsAsync(status, kind, cancellationToken)))
            .WithName("ListMarketingLeads");

        platform.MapPut("/leads/{id:guid}", async (Guid id, MarketingLeadStatusWrite request, IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Ok(await growth.UpdateLeadStatusAsync(id, request, cancellationToken)))
            .WithName("UpdateMarketingLeadStatus");

        platform.MapGet("/support", async (string? status, IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Ok(await growth.ListPlatformSupportTicketsAsync(status, cancellationToken)))
            .WithName("ListPlatformSupportTickets");

        platform.MapPut("/support/{id:guid}", async (Guid id, PlatformSupportUpdateWrite request, IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Ok(await growth.UpdateSupportTicketAsync(id, request, cancellationToken)))
            .WithName("UpdatePlatformSupportTicket");

        platform.MapGet("/analytics", async (IGrowthService growth, CancellationToken cancellationToken) =>
                Results.Ok(await growth.GetSaaSAnalyticsAsync(cancellationToken)))
            .WithName("GetSaaSAnalytics");
    }
}

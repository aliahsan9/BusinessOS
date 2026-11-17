using BusinessOS.Application.Features.Experience;

namespace BusinessOS.API.Endpoints;

public static class ExperienceEndpoints
{
    public static void MapExperienceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/experience")
            .WithTags("Experience")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/dashboard", async (IExperienceService service, CancellationToken ct) => Results.Ok(await service.GetDashboardAsync(ct)));
        group.MapPut("/dashboard/layout", async (DashboardLayoutRequest request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.SaveLayoutAsync(request.Widgets ?? [], ct)));
        group.MapDelete("/dashboard/layout", async (IExperienceService service, CancellationToken ct) => Results.Ok(await service.RestoreLayoutAsync(ct)));
        group.MapGet("/workspace", async (IExperienceService service, CancellationToken ct) => Results.Ok(await service.GetWorkspaceAsync(ct)));
        group.MapPut("/workspace", async (WorkspaceWriteRequest request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.SaveWorkspaceAsync(request.Workspace, ct)));

        var storefront = app.MapGroup("/storefront")
            .WithTags("Storefront")
            .RequireAuthorization()
            .RequireRateLimiting("api");
        storefront.MapGet("/", async (IExperienceService service, CancellationToken ct) => Results.Ok(await service.GetStorefrontAsync(ct)));
        storefront.MapPut("/", async (StorefrontWriteRequest request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.SaveStorefrontAsync(request, ct)));
        storefront.MapPost("/offerings", async (StorefrontOfferingWrite request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.SaveOfferingAsync(request, ct)));
        storefront.MapDelete("/offerings/{id:guid}", async (Guid id, IExperienceService service, CancellationToken ct) => { await service.DeleteOfferingAsync(id, ct); return Results.NoContent(); });
        storefront.MapPost("/packages", async (StorefrontPackageWrite request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.SavePackageAsync(request, ct)));
        storefront.MapDelete("/packages/{id:guid}", async (Guid id, IExperienceService service, CancellationToken ct) => { await service.DeletePackageAsync(id, ct); return Results.NoContent(); });
        storefront.MapPost("/portfolio", async (StorefrontPortfolioWrite request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.SavePortfolioAsync(request, ct)));
        storefront.MapDelete("/portfolio/{id:guid}", async (Guid id, IExperienceService service, CancellationToken ct) => { await service.DeletePortfolioAsync(id, ct); return Results.NoContent(); });
        storefront.MapPost("/reviews/{id:guid}", async (Guid id, ReviewModerationRequest request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.ModerateReviewAsync(id, request.Status, ct)));
        storefront.MapPost("/supplier-links", async (SupplierLinkRequest request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.CreateSupplierLinkAsync(request.SupplierId, request.Email, ct)));
        storefront.MapPost("/supplier-links/{id:guid}/revoke", async (Guid id, IExperienceService service, CancellationToken ct) => { await service.RevokeSupplierLinkAsync(id, ct); return Results.NoContent(); });

        var publicGroup = app.MapGroup("/public")
            .WithTags("Public storefront")
            .AllowAnonymous()
            .RequireRateLimiting("public");
        publicGroup.MapGet("/companies/{slug}", async (string slug, IExperienceService service, CancellationToken ct) =>
        {
            var company = await service.GetPublicCompanyAsync(slug, ct);
            return company is null ? Results.NotFound() : Results.Ok(company);
        });
        publicGroup.MapPost("/companies/{slug}/inquiries", async (string slug, PublicInquiryRequest request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.SubmitInquiryAsync(slug, request, ct)));
        publicGroup.MapPost("/companies/{slug}/orders", async (string slug, PublicOrderRequest request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.SubmitOrderAsync(slug, request, ct)));
        publicGroup.MapPost("/companies/{slug}/reviews", async (string slug, PublicReviewRequest request, IExperienceService service, CancellationToken ct) => Results.Ok(await service.SubmitReviewAsync(slug, request, ct)));
        publicGroup.MapGet("/sitemap.xml", async (HttpContext http, IExperienceService service, CancellationToken ct) =>
        {
            var origin = $"{http.Request.Scheme}://{http.Request.Host.Value}";
            return Results.Content(await service.BuildSitemapAsync(origin, ct), "application/xml");
        });

        app.MapGet("/supplier-portal/{token}", async (string token, IExperienceService service, CancellationToken ct) => Results.Ok(await service.GetSupplierPortalAsync(token, ct)))
            .WithTags("Supplier portal")
            .AllowAnonymous()
            .RequireRateLimiting("public");
    }
}

public sealed class DashboardLayoutRequest
{
    public List<DashboardWidgetWrite>? Widgets { get; set; }
}

public sealed class WorkspaceWriteRequest
{
    public string Workspace { get; set; } = "";
}

public sealed class ReviewModerationRequest
{
    public string Status { get; set; } = "";
}

public sealed class SupplierLinkRequest
{
    public Guid SupplierId { get; set; }
    public string Email { get; set; } = "";
}

using BusinessOS.Application.Features.Help.DTOs;
using BusinessOS.Application.Features.Help.Services;

namespace BusinessOS.API.Endpoints;

public static class HelpEndpoints
{
    public static void MapHelpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/help")
            .WithTags("Help Center")
            .RequireAuthorization();

        group.MapGet("/faqs", GetFaqs)
            .WithName("GetHelpFaqs")
            .Produces<HelpCenterDto>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetFaqs(
        IHelpService helpService,
        CancellationToken cancellationToken)
    {
        var result = await helpService.GetHelpCenterAsync(cancellationToken);
        return Results.Ok(result);
    }
}

using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.DataImports.Commands.CommitDataImport;
using BusinessOS.Application.Features.DataImports.Commands.PreflightDataImport;
using BusinessOS.Application.Features.DataImports.Commands.PreviewDataImport;
using BusinessOS.Application.Features.DataImports.Models;
using BusinessOS.Application.Features.DataImports.Queries.GetDataImportHistory;
using BusinessOS.Application.Features.DataImports.Queries.GetFailedImportRows;
using BusinessOS.Application.Features.DataImports.Queries.GetImportTemplate;
using BusinessOS.API.Authorization;
using MediatR;

namespace BusinessOS.API.Endpoints;

public static class DataImportEndpoints
{
    public static void MapDataImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/data-imports")
            .WithTags("Data Imports")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/templates/{entityType}", DownloadTemplate)
            .RequirePermission(PermissionCodes.SettingsView)
            .WithName("GetDataImportTemplate")
            .WithSummary("Download CSV or XLSX import template")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/preview", PreviewImport)
            .RequirePermission(PermissionCodes.SettingsUpdate)
            .DisableAntiforgery()
            .WithName("PreviewDataImport")
            .WithSummary("Upload CSV/XLSX and preview mapped import rows")
            .Produces<ImportPreviewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/preflight", PreflightImport)
            .RequirePermission(PermissionCodes.SettingsUpdate)
            .WithName("PreflightDataImport")
            .WithSummary("Check plan capacity before committing an import")
            .Produces<ImportPreflightResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/commit", CommitImport)
            .RequirePermission(PermissionCodes.SettingsUpdate)
            .WithName("CommitDataImport")
            .WithSummary("Commit a validated import preview")
            .Produces<DataImportCommitResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/history", GetHistory)
            .RequirePermission(PermissionCodes.SettingsView)
            .WithName("GetDataImportHistory")
            .WithSummary("List recent import batches for the current tenant")
            .Produces<IReadOnlyList<DataImportHistoryItemDto>>(StatusCodes.Status200OK);

        group.MapGet("/{batchId:guid}/failed-rows", DownloadFailedRows)
            .RequirePermission(PermissionCodes.SettingsView)
            .WithName("GetDataImportFailedRows")
            .WithSummary("Download failed rows CSV for an import batch")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> DownloadTemplate(
        string entityType,
        string? format,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var bytes = await sender.Send(new GetImportTemplateQuery(entityType, format), cancellationToken);
        var isXlsx = string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase);
        var fileName = isXlsx
            ? $"{entityType}-import-template.xlsx"
            : $"{entityType}-import-template.csv";
        var contentType = isXlsx
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        return Results.File(bytes, contentType, fileName);
    }

    private static async Task<IResult> PreviewImport(
        HttpRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
            return Results.Problem(
                detail: "Expected multipart/form-data upload.",
                statusCode: StatusCodes.Status400BadRequest);

        var form = await request.ReadFormAsync(cancellationToken);
        var entityType = form["entityType"].ToString();
        var file = form.Files.GetFile("file");

        if (file is null || file.Length == 0)
        {
            return Results.Problem(
                detail: "CSV or XLSX file is required in form field 'file'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);

        var response = await sender.Send(
            new PreviewDataImportCommand(entityType, file.FileName, ms.ToArray()),
            cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> PreflightImport(
        ImportPreflightRequest body,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new PreflightDataImportCommand(body), cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CommitImport(
        CommitDataImportRequest body,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new CommitDataImportCommand(body), cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetHistory(
        int? limit,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new GetDataImportHistoryQuery(limit ?? 25), cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> DownloadFailedRows(
        Guid batchId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var (content, fileName) = await sender.Send(new GetFailedImportRowsQuery(batchId), cancellationToken);
        return Results.File(content, "text/csv", fileName);
    }
}

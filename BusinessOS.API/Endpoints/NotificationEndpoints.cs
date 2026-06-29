using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.API.Authorization;

namespace BusinessOS.API.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("", GetNotifications)
            .RequirePermission(PermissionCodes.NotificationView)
            .WithName("GetNotifications")
            .Produces<PagedResult<NotificationResponse>>(StatusCodes.Status200OK);

        group.MapGet("/unread-count", GetUnreadCount)
            .RequirePermission(PermissionCodes.NotificationView)
            .WithName("GetUnreadNotificationCount")
            .Produces<UnreadCountResponse>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/read", MarkRead)
            .RequirePermission(PermissionCodes.NotificationUpdate)
            .WithName("MarkNotificationRead")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/read/{id:guid}", MarkReadPut)
            .RequirePermission(PermissionCodes.NotificationUpdate)
            .WithName("MarkNotificationReadPut")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/read-all", MarkAllRead)
            .RequirePermission(PermissionCodes.NotificationUpdate)
            .WithName("MarkAllNotificationsRead")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPut("/read-all", MarkAllReadPut)
            .RequirePermission(PermissionCodes.NotificationUpdate)
            .WithName("MarkAllNotificationsReadPut")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/{id:guid}", DeleteNotification)
            .RequirePermission(PermissionCodes.NotificationUpdate)
            .WithName("DeleteNotification")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/preferences", GetPreferences)
            .RequirePermission(PermissionCodes.NotificationView)
            .WithName("GetNotificationPreferences")
            .Produces<NotificationPreferencesDto>(StatusCodes.Status200OK);

        group.MapPut("/preferences", UpdatePreferences)
            .RequirePermission(PermissionCodes.NotificationUpdate)
            .WithName("UpdateNotificationPreferences")
            .Produces<NotificationPreferencesDto>(StatusCodes.Status200OK);

        group.MapPost("", CreateNotification)
            .RequirePermission(PermissionCodes.NotificationUpdate)
            .WithName("CreateNotification")
            .Produces<NotificationResponse>(StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetNotifications(
        bool? unreadOnly,
        int page,
        int pageSize,
        INotificationService notificationService,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new Application.Common.Exceptions.UnauthorizedException("User context is required.");

        var result = await notificationService.GetForUserAsync(
            userId, unreadOnly, page, pageSize, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetUnreadCount(
        INotificationService notificationService,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new Application.Common.Exceptions.UnauthorizedException("User context is required.");

        var count = await notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Results.Ok(new UnreadCountResponse(count));
    }

    private static async Task<IResult> MarkRead(
        Guid id,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        await notificationService.MarkReadAsync(id, cancellationToken);
        return Results.NoContent();
    }

    private static Task<IResult> MarkReadPut(
        Guid id,
        INotificationService notificationService,
        CancellationToken cancellationToken) =>
        MarkRead(id, notificationService, cancellationToken);

    private static async Task<IResult> MarkAllRead(
        INotificationService notificationService,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new Application.Common.Exceptions.UnauthorizedException("User context is required.");

        await notificationService.MarkAllReadAsync(userId, cancellationToken);
        return Results.NoContent();
    }

    private static Task<IResult> MarkAllReadPut(
        INotificationService notificationService,
        ICurrentUserService currentUserService,
        CancellationToken cancellationToken) =>
        MarkAllRead(notificationService, currentUserService, cancellationToken);

    private static async Task<IResult> DeleteNotification(
        Guid id,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        await notificationService.DeleteAsync(id, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetPreferences(
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var result = await notificationService.GetPreferencesAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdatePreferences(
        UpdateNotificationPreferencesRequest request,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var result = await notificationService.UpdatePreferencesAsync(request, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateNotification(
        CreateNotificationRequest request,
        INotificationService notificationService,
        CancellationToken cancellationToken)
    {
        var result = await notificationService.CreateNotificationAsync(request, cancellationToken);
        return Results.Created($"/api/notifications/{result.Id}", result);
    }

    private sealed record UnreadCountResponse(int Count);
}

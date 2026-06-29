using BusinessOS.Application.Common.Models;

namespace BusinessOS.Application.Features.Activities.DTOs;

public class ActivityResponse
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string EntityName { get; set; } = default!;
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Description { get; set; } = default!;
}

public record ActivityQueryParams(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Action = null,
    string? EntityType = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null);

public record LogActivityRequest(
    string Action,
    string EntityType,
    Guid EntityId,
    string EntityName,
    string? Metadata = null);

public record BusinessEventRequest(
    string Action,
    string EntityType,
    Guid EntityId,
    string EntityName,
    string NotificationTitle,
    string NotificationMessage,
    string NotificationType,
    string? Metadata = null,
    string? Link = null);

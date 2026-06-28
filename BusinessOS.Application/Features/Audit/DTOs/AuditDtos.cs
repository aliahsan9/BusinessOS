namespace BusinessOS.Application.Features.Audit.DTOs;

public class AuditLogResponse
{
    public Guid Id { get; set; }
    public string ActorUserId { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class UserOnboardingProgress : AuditableEntity
{
    public string UserId { get; set; } = default!;
    public int CurrentStep { get; set; } = 1;
    public bool IsCompleted { get; set; }
    public bool IsSkipped { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>JSON array of completed step numbers for resume UI.</summary>
    public string? CompletedStepsJson { get; set; }
    public DateTime? LastResumedAt { get; set; }
}

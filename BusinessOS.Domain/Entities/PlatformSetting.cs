namespace BusinessOS.Domain.Entities;

/// <summary>
/// Non-secret platform configuration. API keys stay in the server environment and are never stored here.
/// </summary>
public class PlatformSetting
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedByUserId { get; set; }
}

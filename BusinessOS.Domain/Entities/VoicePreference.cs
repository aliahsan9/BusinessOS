using BusinessOS.Domain.Common;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Per-user voice and spoken-language preferences for AI employee interactions.
/// </summary>
public class VoicePreference : AuditableEntity
{
    public Guid TenantId { get; set; }

    public string UserId { get; set; } = default!;

    public AgentVoiceLanguage Language { get; set; } = AgentVoiceLanguage.En;

    public string VoiceName { get; set; } = "default";

    /// <summary>Speech rate multiplier (1.0 = normal).</summary>
    public double SpeechRate { get; set; } = 1.0;

    /// <summary>Voice pitch adjustment (1.0 = normal).</summary>
    public double Pitch { get; set; } = 1.0;

    public bool ContinuousListening { get; set; }

    public bool AutoSpeak { get; set; } = true;

    /// <summary>Preferred AI employee key (e.g. sophia).</summary>
    public string? PreferredAgentKey { get; set; }
}

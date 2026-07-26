using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Catalog entry for an AI employee agent.
/// When <see cref="TenantId"/> is null the profile is a system default;
/// a non-null tenant id represents a tenant-specific override.
/// </summary>
public class AgentProfile : AuditableEntity
{
    /// <summary>
    /// Null for system catalog defaults; set for tenant-specific overrides.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>Stable machine key (e.g. sophia, adam, emma).</summary>
    public string Key { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string RoleTitle { get; set; } = default!;

    public string Specialty { get; set; } = default!;

    /// <summary>System prompt that defines this employee's persona and tone.</summary>
    public string SystemPersonaPrompt { get; set; } = default!;

    /// <summary>Default language code (en / ur).</summary>
    public string DefaultLanguage { get; set; } = "en";

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
}

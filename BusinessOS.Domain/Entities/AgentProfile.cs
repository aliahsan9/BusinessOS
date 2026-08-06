using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Catalog entry defining an AI employee agent persona, role, and system prompt.
/// </summary>
/// <remarks>
/// When <see cref="TenantId"/> is null the profile is a system-wide default.
/// A non-null tenant id represents a tenant-specific override of the catalog entry.
/// </remarks>
public class AgentProfile : AuditableEntity
{
    /// <summary>
    /// Tenant identifier when this profile is a tenant-specific override; null for system catalog defaults.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Stable machine key used to reference the agent (for example, sophia, adam, emma). Must be unique within scope.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// Human-readable display name shown in the UI for this AI employee.
    /// </summary>
    public string DisplayName { get; set; } = default!;

    /// <summary>
    /// Job title or role label presented to users (for example, Sales Assistant).
    /// </summary>
    public string RoleTitle { get; set; } = default!;

    /// <summary>
    /// Area of expertise or functional specialty for this agent.
    /// </summary>
    public string Specialty { get; set; } = default!;

    /// <summary>
    /// System prompt that defines this employee's persona, tone, and behavioral constraints.
    /// </summary>
    public string SystemPersonaPrompt { get; set; } = default!;

    /// <summary>
    /// Default language code for agent responses (for example, en).
    /// </summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>
    /// Indicates whether this profile is the default agent selection within its scope.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Indicates whether this agent profile is available for use. Defaults to true.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

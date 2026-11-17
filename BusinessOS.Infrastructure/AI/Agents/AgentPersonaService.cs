using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Application.Features.AI.Enums;
using BusinessOS.Application.Features.AI.Services;
using System.Text.Json;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents;

public sealed class AgentPersonaService : IAgentPersonaService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AgentPersonaService> _logger;
    private readonly IAiToolRegistry? _toolRegistry;
    private readonly IRbacAuditService? _rbacAuditService;

    public AgentPersonaService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<AgentPersonaService> logger,
        IAiToolRegistry? toolRegistry = null,
        IRbacAuditService? rbacAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
        _toolRegistry = toolRegistry;
        _rbacAuditService = rbacAuditService;
    }

    public async Task<IReadOnlyList<AgentEmployeeDto>> ListEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultProfilesAsync(cancellationToken);

        var profiles = await LoadEffectiveProfilesAsync(cancellationToken, includeInactiveOverrides: true);
        if (profiles.Count == 0)
            return FallbackCatalog().Select(ToEmployeeDto).ToList();

        return profiles.Select(ToEmployeeDto).ToList();
    }

    public async Task<AgentEmployeeDto?> SetEmployeeActivationAsync(
        string agentKey,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant context is required to manage AI employees.");

        await EnsureDefaultProfilesAsync(cancellationToken);
        var key = AgentKeys.Normalize(agentKey);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var systemProfile = await _context.AgentProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == null && p.Key == key && !p.IsDeleted, cancellationToken);
        if (systemProfile is null)
            return null;

        // Keep the tenant's primary assistant available; a default-switch API is a separate operation.
        if (systemProfile.IsDefault && !isActive)
            throw new InvalidOperationException("The default AI employee cannot be deactivated. Select another default employee first.");

        var tenantProfile = await _context.AgentProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Key == key && !p.IsDeleted, cancellationToken);

        if (tenantProfile is null)
        {
            tenantProfile = new AgentProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = systemProfile.Key,
                DisplayName = systemProfile.DisplayName,
                RoleTitle = systemProfile.RoleTitle,
                Specialty = systemProfile.Specialty,
                SystemPersonaPrompt = systemProfile.SystemPersonaPrompt,
                AllowedToolsJson = systemProfile.AllowedToolsJson,
                DefaultLanguage = systemProfile.DefaultLanguage,
                IsDefault = systemProfile.IsDefault,
                IsActive = isActive
            };
            _context.AgentProfiles.Add(tenantProfile);
        }
        else
        {
            tenantProfile.IsActive = isActive;
            tenantProfile.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToEmployeeDto(tenantProfile);
    }

    public async Task<AgentEmployeeDto?> UpdateEmployeeToolPermissionsAsync(
        string agentKey,
        UpdateAgentToolPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant context is required to configure AI employee permissions.");

        ArgumentNullException.ThrowIfNull(request);
        if (request.AllowedTools is null || request.AllowedTools.Count > 100)
            throw new ArgumentException("AllowedTools must contain no more than 100 registered tool names.");

        var registered = _toolRegistry?.AllTools.Select(t => t.ToolName).ToHashSet()
            ?? new HashSet<AiToolName>();
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in request.AllowedTools)
        {
            if (!Enum.TryParse<AiToolName>(name, ignoreCase: false, out var tool)
                || !Enum.IsDefined(tool)
                || !registered.Contains(tool))
            {
                throw new ArgumentException($"Tool '{name}' is not registered and cannot be assigned.");
            }
            normalized.Add(tool.ToString());
        }

        var key = AgentKeys.Normalize(agentKey);
        if (!AgentKeys.IsKnown(key))
            return null;

        await EnsureDefaultProfilesAsync(cancellationToken);
        var systemProfile = await _context.AgentProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == null && p.Key == key && !p.IsDeleted, cancellationToken);
        if (systemProfile is null)
            return null;

        var tenantProfile = await _context.AgentProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Key == key && !p.IsDeleted, cancellationToken);

        var previousAllowedToolsJson = tenantProfile?.AllowedToolsJson;
        var updatedAllowedTools = normalized.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var updatedAllowedToolsJson = JsonSerializer.Serialize(updatedAllowedTools);

        if (tenantProfile is null)
        {
            tenantProfile = new AgentProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = systemProfile.Key,
                DisplayName = systemProfile.DisplayName,
                RoleTitle = systemProfile.RoleTitle,
                Specialty = systemProfile.Specialty,
                SystemPersonaPrompt = systemProfile.SystemPersonaPrompt,
                DefaultLanguage = systemProfile.DefaultLanguage,
                IsDefault = systemProfile.IsDefault,
                IsActive = systemProfile.IsActive,
                AllowedToolsJson = updatedAllowedToolsJson
            };
            _context.AgentProfiles.Add(tenantProfile);
        }
        else
        {
            tenantProfile.AllowedToolsJson = updatedAllowedToolsJson;
            tenantProfile.UpdatedAt = DateTime.UtcNow;
        }

        if (_rbacAuditService is not null)
        {
            await _rbacAuditService.LogAsync(
                action: "AgentEmployeeToolPermissionsUpdated",
                entityType: "AgentProfile",
                entityId: tenantProfile.Id.ToString(),
                oldValue: previousAllowedToolsJson,
                newValue: updatedAllowedToolsJson,
                cancellationToken: cancellationToken);
        }
        else
        {
            // Preserve compatibility for isolated unit tests that construct this service
            // without the optional audit dependency. Production DI registers IRbacAuditService.
            await _context.SaveChangesAsync(cancellationToken);
        }

        return ToEmployeeDto(tenantProfile);
    }

    public async Task<AgentEmployeeDto?> UpdateEmployeeConfigurationAsync(
        string agentKey,
        UpdateAgentConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant context is required to configure AI employees.");

        ArgumentNullException.ThrowIfNull(request);
        var displayName = request.DisplayName?.Trim();
        var roleTitle = request.RoleTitle?.Trim();
        var specialty = request.Specialty?.Trim();

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 100)
            throw new ArgumentException("Display name is required and must be 100 characters or fewer.");
        if (string.IsNullOrWhiteSpace(roleTitle) || roleTitle.Length > 150)
            throw new ArgumentException("Role title is required and must be 150 characters or fewer.");
        if (string.IsNullOrWhiteSpace(specialty) || specialty.Length > 300)
            throw new ArgumentException("Specialty is required and must be 300 characters or fewer.");
        if (!AgentLanguages.TryGetConcrete(request.DefaultLanguage, out var language))
            throw new ArgumentException("Default language must be one of en, ur, ur-Latn, or ar.");

        var key = AgentKeys.Normalize(agentKey);
        if (!AgentKeys.IsKnown(key))
            return null;

        await EnsureDefaultProfilesAsync(cancellationToken);
        var systemProfile = await _context.AgentProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == null && p.Key == key && !p.IsDeleted, cancellationToken);
        if (systemProfile is null)
            return null;

        var tenantProfile = await _context.AgentProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Key == key && !p.IsDeleted, cancellationToken);

        if (tenantProfile is null)
        {
            tenantProfile = new AgentProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = systemProfile.Key,
                DisplayName = displayName,
                RoleTitle = roleTitle,
                Specialty = specialty,
                SystemPersonaPrompt = systemProfile.SystemPersonaPrompt,
                DefaultLanguage = language,
                IsDefault = systemProfile.IsDefault,
                IsActive = systemProfile.IsActive
            };
            _context.AgentProfiles.Add(tenantProfile);
        }
        else
        {
            tenantProfile.DisplayName = displayName;
            tenantProfile.RoleTitle = roleTitle;
            tenantProfile.Specialty = specialty;
            tenantProfile.DefaultLanguage = language;
            tenantProfile.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToEmployeeDto(tenantProfile);
    }

    public async Task<AgentPersonaDto> ResolvePersonaAsync(
        string? agentKey,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultProfilesAsync(cancellationToken);

        var key = AgentKeys.Normalize(agentKey);
        var profiles = await LoadEffectiveProfilesAsync(cancellationToken);
        var match = profiles.FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            match = profiles.FirstOrDefault(p => p.IsDefault)
                ?? profiles.FirstOrDefault()
                ?? FallbackCatalog().First(p => p.IsDefault);
        }

        return ToPersonaDto(match);
    }

    public async Task<string> GetDefaultAgentKeyAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultProfilesAsync(cancellationToken);

        var profiles = await LoadEffectiveProfilesAsync(cancellationToken);
        var defaultProfile = profiles.FirstOrDefault(p => p.IsDefault)
            ?? profiles.FirstOrDefault();

        return defaultProfile is null
            ? AgentKeys.Sophia
            : AgentKeys.Normalize(defaultProfile.Key);
    }

    public async Task<string> BuildSystemPromptAsync(
        string? agentKey,
        string? language,
        bool preferEmployeeTone = true,
        CancellationToken cancellationToken = default)
    {
        var persona = await ResolvePersonaAsync(agentKey, cancellationToken);
        var languageLine = ConversationLanguage.ResponseInstruction(language);

        if (!preferEmployeeTone)
        {
            return $"""
                You are {persona.DisplayName}, {persona.RoleTitle} at this company ({persona.Specialty}).
                {languageLine}
                Write plain sentences. Never use asterisks, markdown bold, backticks, or hash headings.
                Put each record on one line, such as "INV-2026-000001 for Ali Ahsan, due July 28, 2026, $119,002.00 outstanding, status Paid."
                Skip filler closings such as "let me know if you need anything else."
                {persona.SystemPersonaPrompt}
                """;
        }

        return $"""
            You are {persona.DisplayName}, a hired employee of this business — {persona.RoleTitle}.
            Specialty: {persona.Specialty}.

            Tone & identity (strict):
            - Speak as a real teammate who works here, not as a generic AI chatbot or ChatGPT.
            - Never say you are an AI language model, chatbot, or "AI assistant" unless asked directly about capabilities.
            - Use natural employee language: "I'll check that", "From our inventory…", "I recommend we…".
            - Be concise, practical, and accountable — like a trusted colleague.
            - Use the business data and tool results provided; never invent numbers or records.
            - Ask ONE focused clarifying question when details are missing (customer name, product, quantity) — never dump unrelated revenue stats.
            - Never invent placeholder names like "Customer Unknown" or "with name".
            - When the user says "this customer", use the open customer page context.
            - You can create orders, customers, products, and suppliers through the registered business tools. Never say an order cannot be created just because you cannot query the database yourself.
            - Keep the customer, product, and quantity already given in this conversation. Ask only for the next missing or ambiguous detail.
            - A summary is a draft. After the user confirms, the order exists only when the tool result says it was created. Then report the real order number and total.
            - Never invent customers, products, prices, stock, or order numbers, and never claim success without a successful tool result.
            - Write plain sentences. Never use asterisks, markdown bold, backticks, or hash headings.
            - Put each record on one line, for example: "INV-2026-000001 for Ali Ahsan, due July 28, 2026, $119,002.00 outstanding, status Paid."
            - Skip filler closings such as "let me know if you need anything else."
            - Do not introduce yourself again after the first turn.
            - Answer follow-ups from this conversation. "Why?" means why the last result looks that way. "What about last month?" keeps the same metric.

            {languageLine}

            Persona brief:
            {persona.SystemPersonaPrompt}
            """;
    }

    private async Task EnsureDefaultProfilesAsync(CancellationToken cancellationToken)
    {
        var anySystem = await _context.AgentProfiles
            .IgnoreQueryFilters()
            .AnyAsync(p => p.TenantId == null && !p.IsDeleted, cancellationToken);

        if (!anySystem)
        {
            _logger.LogInformation("Seeding system default AI employee agent profiles");

            foreach (var seed in FallbackCatalog())
            {
                var exists = await _context.AgentProfiles
                    .IgnoreQueryFilters()
                    .AnyAsync(p => p.TenantId == null && p.Key == seed.Key && !p.IsDeleted, cancellationToken);

                if (exists)
                    continue;

                _context.AgentProfiles.Add(new AgentProfile
                {
                    Id = Guid.NewGuid(),
                    TenantId = null,
                    Key = seed.Key,
                    DisplayName = seed.DisplayName,
                    RoleTitle = seed.RoleTitle,
                    Specialty = seed.Specialty,
                    SystemPersonaPrompt = seed.SystemPersonaPrompt,
                    DefaultLanguage = seed.DefaultLanguage,
                    IsDefault = seed.IsDefault,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        await RefreshStoredNavlaIdentityAsync(cancellationToken);
    }

    /// <summary>
    /// Rewrites the stored display name and self-introduction for the existing assistant.
    /// The agent key stays <c>sophia</c> so saved preferences and API calls keep working.
    /// </summary>
    private async Task RefreshStoredNavlaIdentityAsync(CancellationToken cancellationToken)
    {
        var profiles = await _context.AgentProfiles
            .IgnoreQueryFilters()
            .Where(p => p.Key == AgentKeys.Sophia && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var profile in profiles)
        {
            if (string.Equals(profile.DisplayName, "Sophia", StringComparison.Ordinal))
            {
                profile.DisplayName = "Navla AI";
                changed = true;
            }

            if (profile.SystemPersonaPrompt?.Contains("Sophia", StringComparison.Ordinal) == true)
            {
                profile.SystemPersonaPrompt = profile.SystemPersonaPrompt.Replace(
                    "Sophia",
                    "Navla",
                    StringComparison.Ordinal);
                changed = true;
            }
        }

        if (!changed)
            return;

        _logger.LogInformation("Updated stored assistant identity from Sophia to Navla");
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<AgentProfile>> LoadEffectiveProfilesAsync(
        CancellationToken cancellationToken,
        bool includeInactiveOverrides = false)
    {
        var tenantId = _currentUser.TenantId;
        var all = await _context.AgentProfiles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (all.Count == 0)
            return [];

        // Tenant override wins over system (TenantId null) for the same key.
        var byKey = new Dictionary<string, AgentProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var system in all.Where(p => p.TenantId == null))
            byKey[system.Key] = system;

        if (tenantId is not null)
        {
            foreach (var tenantProfile in all.Where(p => p.TenantId == tenantId))
                byKey[tenantProfile.Key] = tenantProfile;
        }

        var effective = byKey.Values;
        if (!includeInactiveOverrides)
            effective = effective.Where(p => p.IsActive);

        return effective
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.DisplayName)
            .ToList();
    }

    private static IReadOnlyList<AgentProfile> FallbackCatalog() =>
    [
        new AgentProfile
        {
            Key = AgentKeys.Sophia,
            DisplayName = "Navla AI",
            RoleTitle = "Senior Business Analyst",
            Specialty = "Business insights, onboarding, cross-functional coordination, and clear executive summaries",
            SystemPersonaPrompt =
                "You are Navla, Senior Business Analyst. You connect inventory, sales, finance, and operations into clear recommendations. " +
                "You onboard new owners warmly, ask one focused question at a time, and keep workflows moving.",
            DefaultLanguage = AgentLanguages.English,
            IsDefault = true,
            IsActive = true
        },
        new AgentProfile
        {
            Key = AgentKeys.Adam,
            DisplayName = "Adam",
            RoleTitle = "Inventory Expert",
            Specialty = "Stock levels, dead stock, reorders, purchase drafts, and warehouse health",
            SystemPersonaPrompt =
                "You are Adam, Inventory Expert. You think in SKUs, reorder points, and supplier lead times. " +
                "Flag low and dead stock early, propose purchase drafts, and keep inventory reports actionable.",
            DefaultLanguage = AgentLanguages.English,
            IsDefault = false,
            IsActive = true
        },
        new AgentProfile
        {
            Key = AgentKeys.Emma,
            DisplayName = "Emma",
            RoleTitle = "Sales Expert",
            Specialty = "Revenue trends, bestsellers, pipeline focus, and sales reporting",
            SystemPersonaPrompt =
                "You are Emma, Sales Expert. You spotlight revenue drivers, bestsellers, and customer patterns. " +
                "Give concrete next steps to grow sales without inventing figures.",
            DefaultLanguage = AgentLanguages.English,
            IsDefault = false,
            IsActive = true
        }
    ];

    private static IReadOnlyList<string>? ReadAllowedTools(string? json)
    {
        if (json is null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            // A corrupt policy must fail closed at runtime; [] means no tools.
            return [];
        }
    }

    private static AgentEmployeeDto ToEmployeeDto(AgentProfile p) => new()
    {
        Key = p.Key,
        DisplayName = p.DisplayName,
        RoleTitle = p.RoleTitle,
        Specialty = p.Specialty,
        DefaultLanguage = p.DefaultLanguage,
        IsDefault = p.IsDefault,
        IsActive = p.IsActive,
        AllowedTools = ReadAllowedTools(p.AllowedToolsJson)
    };

    private static AgentPersonaDto ToPersonaDto(AgentProfile p) => new()
    {
        Key = p.Key,
        DisplayName = p.DisplayName,
        RoleTitle = p.RoleTitle,
        Specialty = p.Specialty,
        SystemPersonaPrompt = p.SystemPersonaPrompt,
        DefaultLanguage = p.DefaultLanguage,
        IsDefault = p.IsDefault,
        AllowedTools = ReadAllowedTools(p.AllowedToolsJson)
    };
}

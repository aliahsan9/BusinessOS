using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Application.Features.AI.Enums;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.AI.Agents;

public sealed class AgentPersonaService : IAgentPersonaService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AgentPersonaService> _logger;

    public AgentPersonaService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<AgentPersonaService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
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
                DefaultLanguage = systemProfile.DefaultLanguage,
                AllowedToolsJson = systemProfile.AllowedToolsJson,
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

    public async Task<AgentToolPermissionsDto?> GetToolPermissionsAsync(
        string agentKey,
        CancellationToken cancellationToken = default)
    {
        var key = AgentKeys.Normalize(agentKey);
        var profiles = await LoadEffectiveProfilesAsync(cancellationToken, includeInactiveOverrides: true);
        var profile = profiles.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        return profile is null ? null : ToToolPermissionsDto(profile);
    }

    public async Task<AgentToolPermissionsDto?> UpdateToolPermissionsAsync(
        string agentKey,
        UpdateAgentToolPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("A tenant context is required to manage AI employee permissions.");

        ArgumentNullException.ThrowIfNull(request);
        var key = AgentKeys.Normalize(agentKey);
        if (!AgentKeys.IsKnown(key))
            return null;

        var submitted = request.AllowedTools ?? [];
        if (submitted.Count > 100)
            throw new ArgumentException("An AI employee can have at most 100 allowed tools.");

        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in submitted)
        {
            if (string.IsNullOrWhiteSpace(raw)
                || !Enum.TryParse<AiToolName>(raw.Trim(), ignoreCase: false, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                throw new ArgumentException($"Unknown AI tool: {raw}.");
            }

            normalized.Add(parsed.ToString());
        }

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
                DisplayName = systemProfile.DisplayName,
                RoleTitle = systemProfile.RoleTitle,
                Specialty = systemProfile.Specialty,
                SystemPersonaPrompt = systemProfile.SystemPersonaPrompt,
                DefaultLanguage = systemProfile.DefaultLanguage,
                AllowedToolsJson = systemProfile.AllowedToolsJson,
                IsDefault = systemProfile.IsDefault,
                IsActive = systemProfile.IsActive
            };
            _context.AgentProfiles.Add(tenantProfile);
        }

        tenantProfile.AllowedToolsJson = JsonSerializer.Serialize(normalized.OrderBy(x => x, StringComparer.Ordinal));
        tenantProfile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return ToToolPermissionsDto(tenantProfile);
    }

    public async Task<bool> IsToolAllowedAsync(
        string agentKey,
        AiToolName tool,
        CancellationToken cancellationToken = default)
    {
        var key = AgentKeys.Normalize(agentKey);
        var profiles = await LoadEffectiveProfilesAsync(cancellationToken);
        var profile = profiles.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        if (profile is null || !profile.IsActive)
            return false;

        try
        {
            var allowed = JsonSerializer.Deserialize<string[]>(profile.AllowedToolsJson ?? "[]") ?? [];
            return allowed.Contains(tool.ToString(), StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid tool permission JSON for AI employee {AgentKey}", key);
            return false;
        }
    }

    private static string DefaultAllowedToolsJson(string key)
    {
        // Default permissions are intentionally read-only. Tenant Owners/Admins grant writes explicitly.
        var readOnly = key switch
        {
            AgentKeys.Adam => new[]
            {
                AiToolName.GetProducts, AiToolName.SearchProduct, AiToolName.GetInventorySummary,
                AiToolName.GetLowStock, AiToolName.GetOutOfStock, AiToolName.GetDeadStock,
                AiToolName.GetPurchaseRecommendations, AiToolName.GetInventoryHealth,
                AiToolName.SearchSupplier
            },
            AgentKeys.Emma => new[]
            {
                AiToolName.GetCustomers, AiToolName.SearchCustomer, AiToolName.GetInvoices,
                AiToolName.SearchInvoice, AiToolName.GetRevenue, AiToolName.GetSalesSummary,
                AiToolName.GetBestSellingProducts, AiToolName.GetSalesTrends,
                AiToolName.GetCustomerInsights, AiToolName.GetBusinessHealth,
                AiToolName.GetDailyPriorities, AiToolName.ShowProfit
            },
            _ => new[]
            {
                AiToolName.GetCustomers, AiToolName.GetProjects, AiToolName.GetTasks,
                AiToolName.GetInvoices, AiToolName.GetExpenses, AiToolName.GetProducts,
                AiToolName.GetRevenue, AiToolName.GetSalesSummary, AiToolName.GetBestSellingProducts,
                AiToolName.GetSalesTrends, AiToolName.GetInventorySummary, AiToolName.GetLowStock,
                AiToolName.GetDeadStock, AiToolName.GetPurchaseRecommendations,
                AiToolName.GetBusinessSettings, AiToolName.GetNotificationsSummary,
                AiToolName.SearchDocuments, AiToolName.ShowProfit, AiToolName.SearchCustomer,
                AiToolName.SearchProduct, AiToolName.SearchInvoice, AiToolName.SearchSupplier,
                AiToolName.GetOutOfStock, AiToolName.GetInventoryHealth,
                AiToolName.GetBusinessHealth, AiToolName.GetDailyPriorities, AiToolName.GetCustomerInsights
            }
        };

        return JsonSerializer.Serialize(readOnly.Select(x => x.ToString()));
    }

    private static AgentToolPermissionsDto ToToolPermissionsDto(AgentProfile profile)
    {
        string[] allowed;
        try
        {
            allowed = JsonSerializer.Deserialize<string[]>(profile.AllowedToolsJson ?? "[]") ?? [];
        }
        catch (JsonException)
        {
            allowed = [];
        }

        return new AgentToolPermissionsDto { AgentKey = profile.Key, AllowedTools = allowed };
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
                    AllowedToolsJson = DefaultAllowedToolsJson(seed.Key),
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

    private static AgentEmployeeDto ToEmployeeDto(AgentProfile p) => new()
    {
        Key = p.Key,
        DisplayName = p.DisplayName,
        RoleTitle = p.RoleTitle,
        Specialty = p.Specialty,
        DefaultLanguage = p.DefaultLanguage,
        IsDefault = p.IsDefault,
        IsActive = p.IsActive
    };

    private static AgentPersonaDto ToPersonaDto(AgentProfile p) => new()
    {
        Key = p.Key,
        DisplayName = p.DisplayName,
        RoleTitle = p.RoleTitle,
        Specialty = p.Specialty,
        SystemPersonaPrompt = p.SystemPersonaPrompt,
        DefaultLanguage = p.DefaultLanguage,
        IsDefault = p.IsDefault
    };
}

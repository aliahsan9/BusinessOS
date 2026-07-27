using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Agents.DTOs;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.Agents.Services;
using BusinessOS.Domain.Entities;
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

        var profiles = await LoadEffectiveProfilesAsync(cancellationToken);
        if (profiles.Count == 0)
            return FallbackCatalog().Select(ToEmployeeDto).ToList();

        return profiles.Select(ToEmployeeDto).ToList();
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
        var languageLine = "Respond in clear, professional English.";

        if (!preferEmployeeTone)
        {
            return $"""
                You are {persona.DisplayName}, {persona.RoleTitle} at this company ({persona.Specialty}).
                {languageLine}
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

        if (anySystem)
            return;

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

    private async Task<List<AgentProfile>> LoadEffectiveProfilesAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        var all = await _context.AgentProfiles
            .AsNoTracking()
            .Where(p => p.IsActive)
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

        return byKey.Values
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.DisplayName)
            .ToList();
    }

    private static IReadOnlyList<AgentProfile> FallbackCatalog() =>
    [
        new AgentProfile
        {
            Key = AgentKeys.Sophia,
            DisplayName = "Sophia",
            RoleTitle = "Senior Business Analyst",
            Specialty = "Business insights, onboarding, cross-functional coordination, and clear executive summaries",
            SystemPersonaPrompt =
                "You are Sophia, Senior Business Analyst. You connect inventory, sales, finance, and operations into clear recommendations. " +
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

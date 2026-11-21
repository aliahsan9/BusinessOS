using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.SalesAgent;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.SalesAgent;

public sealed class LeadResearchRouter : ILeadResearchRouter
{
    private readonly IEnumerable<ILeadResearchProvider> _providers;
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;

    public LeadResearchRouter(IEnumerable<ILeadResearchProvider> providers, BusinessOSDbContext db, ITenantProvider tenant)
    {
        _providers = providers;
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<LeadResearchProviderStatus>> ListProvidersAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<LeadResearchProviderStatus>();
        foreach (var provider in _providers)
            list.Add(await provider.GetStatusAsync(cancellationToken));
        return list;
    }

    public async Task<LeadResearchProviderStatus> GetActiveStatusAsync(CancellationToken cancellationToken = default)
    {
        var preferred = await ResolvePreferredAsync(cancellationToken);
        if (preferred is not null)
            return await preferred.GetStatusAsync(cancellationToken);

        return new LeadResearchProviderStatus(
            "none",
            "No research provider",
            false,
            false,
            false,
            "No external research provider is connected. Enable the deterministic fixture for sandbox research, configure an HTTP research API, or import owner-authorized public business records.");
    }

    public async Task<IReadOnlyList<LeadResearchResult>> SearchAsync(LeadResearchQuery query, CancellationToken cancellationToken = default)
    {
        var preferred = await ResolvePreferredAsync(cancellationToken);
        if (preferred is null)
            return [];

        return await preferred.SearchAsync(query, cancellationToken);
    }

    private async Task<ILeadResearchProvider?> ResolvePreferredAsync(CancellationToken ct)
    {
        Domain.Entities.SalesAgentSettings? settings = null;
        if (_tenant.HasTenant())
            settings = await _db.SalesAgentSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        var configured = _providers.FirstOrDefault(x => x.ProviderKey == ConfiguredHttpLeadResearchProvider.Key);
        if (configured is not null)
        {
            var status = await configured.GetStatusAsync(ct);
            if (status.IsAvailable)
                return configured;
        }

        if (settings?.AllowFixtureResearch == true
            || string.Equals(settings?.ResearchProviderKey, FixtureLeadResearchProvider.Key, StringComparison.OrdinalIgnoreCase))
        {
            return _providers.FirstOrDefault(x => x.ProviderKey == FixtureLeadResearchProvider.Key);
        }

        return null;
    }
}

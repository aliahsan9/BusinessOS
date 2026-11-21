using BusinessOS.Application.Features.Integrations;
using BusinessOS.Infrastructure.Integrations.Providers;

namespace BusinessOS.Infrastructure.Integrations;

public sealed class IntegrationProviderRegistry : IIntegrationProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IIntegrationProvider> _map;

    public IntegrationProviderRegistry(IEnumerable<IIntegrationProvider> providers)
    {
        var specialized = providers.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var all = new Dictionary<string, IIntegrationProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in IntegrationProviderCatalog.All)
        {
            if (specialized.TryGetValue(definition.Key, out var provider))
                all[definition.Key] = provider;
            else
                all[definition.Key] = new CatalogIntegrationProvider(definition.Key);
        }

        _map = all;
    }

    public IReadOnlyList<IIntegrationProvider> All => _map.Values.ToList();

    public bool TryGet(string key, out IIntegrationProvider? provider) =>
        _map.TryGetValue(key, out provider);
}

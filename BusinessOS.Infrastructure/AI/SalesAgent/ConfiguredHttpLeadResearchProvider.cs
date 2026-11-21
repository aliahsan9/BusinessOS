using System.Net.Http.Headers;
using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.SalesAgent;
using BusinessOS.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.AI.SalesAgent;

/// <summary>
/// Optional HTTP lead-research adapter. Requires tenant-configured base URL + API key.
/// Never fabricates companies when the provider is disconnected or returns invalid payloads.
/// </summary>
public sealed class ConfiguredHttpLeadResearchProvider : ILeadResearchProvider
{
    public const string Key = "configured-http";
    private readonly BusinessOSDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;

    public ConfiguredHttpLeadResearchProvider(
        BusinessOSDbContext db,
        ITenantProvider tenant,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtection)
    {
        _db = db;
        _tenant = tenant;
        _httpClientFactory = httpClientFactory;
        _protector = dataProtection.CreateProtector("BusinessOS.SalesAgent.ResearchApiKey.v1");
    }

    public string ProviderKey => Key;
    public string DisplayName => "Configured HTTP research API";
    public bool IsFixture => false;
    public bool IsLiveIntegration => true;

    public async Task<LeadResearchProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var settings = await SettingsAsync(cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(settings.ResearchBaseUrl) || string.IsNullOrWhiteSpace(settings.ResearchApiKeyProtected))
        {
            return new LeadResearchProviderStatus(
                ProviderKey,
                DisplayName,
                false,
                false,
                true,
                "No external research provider is connected. Configure a research base URL and API key in Sales Agent settings, enable the deterministic fixture for sandbox use, or import owner-authorized public business records.");
        }

        return new LeadResearchProviderStatus(
            ProviderKey,
            DisplayName,
            true,
            false,
            true,
            $"Connected to configured research endpoint '{settings.ResearchBaseUrl}'. ProviderVerified remains false until a live provider is certified.");
    }

    public async Task<IReadOnlyList<LeadResearchResult>> SearchAsync(LeadResearchQuery query, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(cancellationToken);
        if (!status.IsAvailable)
            return [];

        var settings = await SettingsAsync(cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(settings.ResearchBaseUrl) || string.IsNullOrWhiteSpace(settings.ResearchApiKeyProtected))
            return [];

        string apiKey;
        try
        {
            apiKey = _protector.Unprotect(settings.ResearchApiKeyProtected);
        }
        catch
        {
            return [];
        }

        var client = _httpClientFactory.CreateClient("SalesLeadResearch");
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(settings.ResearchBaseUrl!, query));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return Parse(document.RootElement, settings.ResearchBaseUrl!);
    }

    private async Task<Domain.Entities.SalesAgentSettings?> SettingsAsync(CancellationToken ct)
    {
        if (!_tenant.HasTenant())
            return null;
        return await _db.SalesAgentSettings.AsNoTracking().FirstOrDefaultAsync(ct);
    }

    private static Uri BuildUri(string baseUrl, LeadResearchQuery query)
    {
        var builder = new UriBuilder(baseUrl.TrimEnd('/') + "/prospects");
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Industry)) q.Add("industry=" + Uri.EscapeDataString(query.Industry));
        if (!string.IsNullOrWhiteSpace(query.Country)) q.Add("country=" + Uri.EscapeDataString(query.Country));
        if (!string.IsNullOrWhiteSpace(query.CompanySize)) q.Add("companySize=" + Uri.EscapeDataString(query.CompanySize));
        if (!string.IsNullOrWhiteSpace(query.Keywords)) q.Add("q=" + Uri.EscapeDataString(query.Keywords));
        if (!string.IsNullOrWhiteSpace(query.RequiredServices)) q.Add("services=" + Uri.EscapeDataString(query.RequiredServices));
        q.Add("limit=" + Math.Clamp(query.Limit <= 0 ? 10 : query.Limit, 1, 50));
        builder.Query = string.Join('&', q);
        return builder.Uri;
    }

    private static IReadOnlyList<LeadResearchResult> Parse(JsonElement root, string baseUrl)
    {
        var items = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray()
            : root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray()
                : root.TryGetProperty("prospects", out var prospects) && prospects.ValueKind == JsonValueKind.Array
                    ? prospects.EnumerateArray()
                    : Enumerable.Empty<JsonElement>();

        var results = new List<LeadResearchResult>();
        foreach (var item in items)
        {
            var name = Str(item, "companyName") ?? Str(item, "name") ?? Str(item, "company");
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var email = Str(item, "publicContactEmail") ?? Str(item, "email");
            var website = Str(item, "website") ?? Str(item, "url");
            var sourceUrl = Str(item, "sourceUrl") ?? website;
            if (string.IsNullOrWhiteSpace(sourceUrl) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(website))
                continue;

            results.Add(new LeadResearchResult(
                name.Trim(),
                website,
                Str(item, "industry"),
                Str(item, "country"),
                Str(item, "companySize") ?? Str(item, "size"),
                Str(item, "businessDescription") ?? Str(item, "description"),
                email,
                Str(item, "publicContactPhone") ?? Str(item, "phone"),
                Str(item, "decisionMakerName"),
                Str(item, "decisionMakerTitle"),
                sourceUrl,
                Key,
                $"Configured HTTP research provider ({baseUrl}).",
                DateTime.UtcNow,
                item.GetRawText(),
                item.TryGetProperty("contactVerified", out var verified) && verified.ValueKind == JsonValueKind.True));
        }

        return results;
    }

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

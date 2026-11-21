using System.Text.Json;
using BusinessOS.Application.Features.SalesAgent;

namespace BusinessOS.Infrastructure.AI.SalesAgent;

/// <summary>
/// Deterministic authorized fixture for automated tests and owner-enabled sandbox research.
/// Explicitly labeled as fixture — never presented as a live third-party integration.
/// </summary>
public sealed class FixtureLeadResearchProvider : ILeadResearchProvider
{
    public const string Key = "fixture";

    public string ProviderKey => Key;
    public string DisplayName => "Deterministic test fixture";
    public bool IsFixture => true;
    public bool IsLiveIntegration => false;

    private static readonly LeadResearchResult[] Catalog =
    [
        Prospect("Bright Harbor Software", "https://brightharbor.example", "Software", "United Kingdom", "51-200",
            "Builds internal business systems and needs .NET and Angular specialists for product modernization.",
            "partnerships@brightharbor.example", "UK", "Helen Croft", "CTO"),
        Prospect("Nordic Ledger Labs", "https://nordicledger.example", "FinTech", "Sweden", "11-50",
            "Payments platform seeking Angular front-end and .NET API contractors for open banking work.",
            "hello@nordicledger.example", "SE", "Erik Lind", "Head of Engineering"),
        Prospect("Atlas Retail Tech", "https://atlasretail.example", "Retail Technology", "Germany", "201-500",
            "Omnichannel retailer evaluating custom .NET commerce services and Angular storefronts.",
            "procurement@atlasretail.example", "DE", "Mia Keller", "VP Digital"),
        Prospect("Cedar Health Systems", "https://cedarhealth.example", "Healthcare IT", "Canada", "51-200",
            "Digital health company needing secure .NET backends and Angular clinician portals.",
            "vendors@cedarhealth.example", "CA", "Jordan Blake", "CIO"),
        Prospect("Pacific Trade Cloud", "https://pacifictrade.example", "Logistics Software", "Australia", "11-50",
            "Freight SaaS team looking for .NET microservices and Angular operations dashboards.",
            "partners@pacifictrade.example", "AU", "Sam Nguyen", "Founder"),
        Prospect("Queen City Analytics", "https://queencityanalytics.example", "Data / Analytics", "United Kingdom", "11-50",
            "Analytics consultancy needing Angular visualization apps and .NET data APIs for clients.",
            "enquiries@queencityanalytics.example", "UK", "Priya Shah", "Managing Director"),
        Prospect("Harborline Manufacturing OS", "https://harborline.example", "Industrial Software", "Netherlands", "51-200",
            "MES vendor seeking .NET engineers and Angular UX partners for factory floor modules.",
            "alliances@harborline.example", "NL", "Lars de Vries", "Product Director"),
        Prospect("Summit Legal Platforms", "https://summitlegal.example", "LegalTech", "United States", "51-200",
            "Legal workflow SaaS exploring .NET modernization and Angular matter-management UI.",
            "bd@summitlegal.example", "US", "Alex Rivera", "VP Engineering"),
        Prospect("GreenGrid Energy Apps", "https://greengrid.example", "CleanTech", "United Kingdom", "11-50",
            "Energy monitoring startup requiring Angular dashboards and .NET IoT ingestion services.",
            "hello@greengrid.example", "UK", "Chloe Martin", "CEO"),
        Prospect("Orbit Edu Systems", "https://orbitedu.example", "EdTech", "Ireland", "11-50",
            "Learning platform expanding with Angular learner apps and .NET content services.",
            "partnerships@orbitedu.example", "IE", "Niamh Doyle", "CTO"),
        Prospect("Silverline Insurance Tech", "https://silverlineins.example", "InsurTech", "United Kingdom", "201-500",
            "Insurer digital team seeking Angular portals and .NET policy services partners.",
            "digital@silverlineins.example", "UK", "Tom Hughes", "Digital Lead"),
        Prospect("Cascade Supply Soft", "https://cascadesupply.example", "Supply Chain Software", "United States", "51-200",
            "Supply-chain platform needing .NET integrations and Angular planner workspaces.",
            "partners@cascadesupply.example", "US", "Riley Chen", "COO")
    ];

    public Task<LeadResearchProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new LeadResearchProviderStatus(
            ProviderKey,
            DisplayName,
            true,
            true,
            false,
            "Fixture research is enabled for this company. Results are deterministic sample companies with example.com-style domains — not live third-party data."));

    public Task<IReadOnlyList<LeadResearchResult>> SearchAsync(LeadResearchQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<LeadResearchResult> rows = Catalog;
        if (!string.IsNullOrWhiteSpace(query.Country))
            rows = rows.Where(x => string.Equals(x.Country, query.Country, StringComparison.OrdinalIgnoreCase)
                                   || (x.Country?.Contains(query.Country, StringComparison.OrdinalIgnoreCase) ?? false));
        if (!string.IsNullOrWhiteSpace(query.Industry))
            rows = rows.Where(x => (x.Industry?.Contains(query.Industry, StringComparison.OrdinalIgnoreCase) ?? false)
                                   || (x.BusinessDescription?.Contains(query.Industry, StringComparison.OrdinalIgnoreCase) ?? false));
        if (!string.IsNullOrWhiteSpace(query.Keywords))
        {
            var keys = query.Keywords.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            rows = rows.Where(x => keys.Any(k =>
                (x.BusinessDescription?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                || (x.Industry?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)));
        }
        if (!string.IsNullOrWhiteSpace(query.RequiredServices))
        {
            var services = query.RequiredServices.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            rows = rows.Where(x => services.Any(s => x.BusinessDescription?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (!string.IsNullOrWhiteSpace(query.CompanySize))
            rows = rows.Where(x => MatchesSize(x.CompanySize, query.CompanySize));

        var limit = Math.Clamp(query.Limit <= 0 ? 10 : query.Limit, 1, 50);
        IReadOnlyList<LeadResearchResult> result = rows.Take(limit).ToList();
        return Task.FromResult(result);
    }

    private static bool MatchesSize(string? actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual))
            return false;
        return actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
               || expected.Contains(actual, StringComparison.OrdinalIgnoreCase);
    }

    private static LeadResearchResult Prospect(
        string name,
        string website,
        string industry,
        string country,
        string size,
        string description,
        string email,
        string countryCode,
        string dmName,
        string dmTitle)
    {
        var payload = JsonSerializer.Serialize(new
        {
            fixture = true,
            company = name,
            website,
            industry,
            country,
            countryCode,
            size,
            description,
            email,
            decisionMaker = new { name = dmName, title = dmTitle }
        });
        return new LeadResearchResult(
            name,
            website,
            industry,
            country,
            size,
            description,
            email,
            null,
            dmName,
            dmTitle,
            website + "/about",
            Key,
            "BusinessOS deterministic sales research fixture (authorized test data).",
            DateTime.UtcNow,
            payload,
            false);
    }
}

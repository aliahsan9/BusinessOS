using BusinessOS.Application.Features.SalesAgent;

namespace BusinessOS.Infrastructure.AI.SalesAgent;

public sealed class LeadQualificationService : ILeadQualificationService
{
    public LeadQualificationResult Score(
        IReadOnlyList<QualificationCriterion> criteria,
        int threshold,
        string? industry,
        string? country,
        string? companySize,
        string? businessDescription,
        string? requiredServices,
        string? website,
        string? publicContactEmail)
    {
        if (criteria.Count == 0)
        {
            criteria =
            [
                new("industry", "Industry", null, 20),
                new("country", "Geographic location", null, 15),
                new("companySize", "Company size", null, 10),
                new("services", "Required services relevance", null, 25),
                new("contact", "Public business contact", null, 20),
                new("website", "Company website", null, 10)
            ];
        }

        var matched = new List<string>();
        var missing = new List<string>();
        var earned = 0;
        var total = Math.Max(1, criteria.Sum(x => Math.Max(1, x.Weight)));

        foreach (var criterion in criteria)
        {
            var hit = criterion.Key.ToLowerInvariant() switch
            {
                "industry" => Matches(industry, criterion.ExpectedValue) || ContainsAny(businessDescription, criterion.ExpectedValue),
                "country" or "geography" or "location" => Matches(country, criterion.ExpectedValue),
                "companysize" or "size" => Matches(companySize, criterion.ExpectedValue),
                "services" or "requiredservices" or "demand" =>
                    ContainsAny(businessDescription, criterion.ExpectedValue ?? requiredServices)
                    || ContainsAny(requiredServices, criterion.ExpectedValue)
                    || ContainsAny(industry, criterion.ExpectedValue ?? requiredServices),
                "budget" => ContainsAny(businessDescription, criterion.ExpectedValue ?? "budget enterprise funded series"),
                "contact" => !string.IsNullOrWhiteSpace(publicContactEmail),
                "website" => !string.IsNullOrWhiteSpace(website),
                "relevance" or "businessrelevance" =>
                    ContainsAny(businessDescription, requiredServices)
                    || ContainsAny(businessDescription, criterion.ExpectedValue),
                _ => Matches(businessDescription, criterion.ExpectedValue) || Matches(industry, criterion.ExpectedValue)
            };

            if (hit)
            {
                matched.Add(criterion.Label);
                earned += Math.Max(1, criterion.Weight);
            }
            else
            {
                missing.Add(criterion.Label);
            }
        }

        var score = (int)Math.Round(100d * earned / total);
        var meets = score >= threshold;
        var explanation =
            $"AI-assisted score {score}/100 against configurable criteria (threshold {threshold}). " +
            "This is an estimate, not verified business truth. " +
            (matched.Count > 0 ? $"Matched: {string.Join(", ", matched)}. " : "") +
            (missing.Count > 0 ? $"Gaps: {string.Join(", ", missing)}." : "All configured criteria matched.");

        return new LeadQualificationResult(score, meets, explanation.Trim(), true, matched, missing);
    }

    private static bool Matches(string? actual, string? expected)
    {
        if (string.IsNullOrWhiteSpace(actual))
            return false;
        if (string.IsNullOrWhiteSpace(expected))
            return true;
        return actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
               || expected.Contains(actual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string? haystack, string? needles)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needles))
            return false;
        foreach (var part in needles.Split([',', ';', '|', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (haystack.Contains(part, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

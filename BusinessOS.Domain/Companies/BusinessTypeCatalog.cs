using System.Text.Json;

namespace BusinessOS.Domain.Companies;

/// <summary>
/// Data-backed company templates. New types are rows in <c>BusinessTypeTemplates</c>, seeded from this catalog.
/// </summary>
public static class BusinessTypeCatalog
{
    public const string CustomKey = "custom";

    private static readonly IReadOnlyList<BusinessTypeDefinition> Core =
    [
        new("software-agency", "Software Development Agency", "Projects, delivery, and client billing for a software team.",
            """["projects","tasks","customers","invoices","ai"]""", 10),
        new("it-consultancy", "IT Consultancy", "Advisory work, clients, and professional services billing.",
            """["projects","customers","invoices","ai"]""", 20),
        new("marketing-agency", "Marketing Agency", "Campaign operations, clients, and agency billing.",
            """["customers","projects","invoices","ai"]""", 30),
        new("ecommerce", "E-commerce Business", "Catalog, stock, and online orders.",
            """["products","inventory","orders","customers","payments"]""", 40),
        new("retail", "Retail Business", "In-store products, stock, and point-of-sale records.",
            """["products","inventory","orders","payments"]""", 50),
        new("service", "Service-Based Business", "Clients, jobs, and service invoices.",
            """["customers","projects","tasks","invoices"]""", 60),
        new(CustomKey, "Custom Business", "Starts with every operational module. Adjust the company later.",
            """["products","inventory","orders","customers","payments","invoices","projects","tasks","ai"]""", 70)
    ];

    public static readonly IReadOnlyList<BusinessTypeDefinition> Defaults =
        Core.Concat(AgencyTemplateCatalog.AsBusinessTypes()).OrderBy(x => x.SortOrder).ToArray();
}

public static class ModuleList
{
    public static IReadOnlyList<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed record BusinessTypeDefinition(
    string Key,
    string Name,
    string Description,
    string ModulesJson,
    int SortOrder);

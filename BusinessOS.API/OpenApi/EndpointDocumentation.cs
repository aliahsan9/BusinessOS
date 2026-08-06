namespace BusinessOS.API.OpenApi;

/// <summary>
/// Shared OpenAPI documentation fragments for BusinessOS minimal API endpoints.
/// </summary>
public static class EndpointDocumentation
{
    /// <summary>
    /// Standard authentication note for protected tenant-scoped endpoints.
    /// </summary>
    public const string TenantAuthNote =
        "**Authentication:** Bearer JWT required.\n\n" +
        "**Tenant:** Send `X-Tenant-ID` header matching the JWT `TenantId` claim.\n\n" +
        "**Authorization:** Endpoint requires a specific permission code (see remarks).";

    /// <summary>
    /// Standard public endpoint note (no authentication).
    /// </summary>
    public const string PublicNote =
        "**Authentication:** Not required.\n\n" +
        "**Tenant:** Not applicable.";

    /// <summary>
    /// Appends common HTTP status code documentation to an endpoint description.
    /// </summary>
    public static string WithStatusCodes(string description, params (int Code, string Meaning)[] codes)
    {
        if (codes.Length == 0)
        {
            return description;
        }

        var statusSection = string.Join("\n", codes.Select(c => $"- **{c.Code}** — {c.Meaning}"));
        return $"{description}\n\n**HTTP Status Codes:**\n{statusSection}";
    }

    /// <summary>
    /// Appends a JSON example block to the description for Scalar rendering.
    /// </summary>
    public static string WithExampleRequest(string description, string method, string path, string jsonExample)
    {
        return $"{description}\n\n**Example Request:**\n\n```http\n{method} {path}\n```\n\n```json\n{jsonExample}\n```";
    }

    /// <summary>
    /// Appends a JSON response example block to the description.
    /// </summary>
    public static string WithExampleResponse(string description, string jsonExample)
    {
        return $"{description}\n\n**Example Response:**\n\n```json\n{jsonExample}\n```";
    }

    /// <summary>
    /// Appends business rules section to the description.
    /// </summary>
    public static string WithBusinessRules(string description, params string[] rules)
    {
        if (rules.Length == 0)
        {
            return description;
        }

        var rulesSection = string.Join("\n", rules.Select(r => $"- {r}"));
        return $"{description}\n\n**Business Rules:**\n{rulesSection}";
    }
}

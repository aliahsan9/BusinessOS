using System.Text.Json;

namespace BusinessOS.Infrastructure.AI;

/// <summary>
/// Allowlisted arguments the model may send with a read tool.
/// Tenant identity, SQL, and secrets are never accepted from the model.
/// </summary>
public static class SophiaToolArguments
{
    public const string ReadParametersJson = """
        {"type":"object","additionalProperties":false,"properties":{"period":{"type":"string","enum":["today","yesterday","this_week","last_week","this_month","last_month","last_3_months","this_year","last_year"],"description":"Date range the user named. Omit when they did not name one."},"query":{"type":"string","description":"Person, product, or SKU named by the user. A short name only. Never a tenant id, SQL statement, or secret."}}}
        """;

    private static readonly HashSet<string> Periods = new(StringComparer.Ordinal)
    {
        "today", "yesterday", "this_week", "last_week", "this_month", "last_month",
        "last_3_months", "this_year", "last_year"
    };

    public static JsonElement Sanitize(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return Empty();

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return Empty();

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (IsBlockedProperty(property.Name))
                        continue;

                    if (property.Name.Equals("period", StringComparison.OrdinalIgnoreCase))
                    {
                        if (property.Value.ValueKind != JsonValueKind.String)
                            continue;
                        var period = property.Value.GetString();
                        if (period is null || !Periods.Contains(period))
                            continue;
                        writer.WriteString("period", period);
                        continue;
                    }

                    if (property.Name.Equals("query", StringComparison.OrdinalIgnoreCase))
                    {
                        if (property.Value.ValueKind != JsonValueKind.String)
                            continue;
                        var query = property.Value.GetString()?.Trim();
                        if (string.IsNullOrWhiteSpace(query) || LooksLikeSql(query))
                            continue;
                        if (query.Length > 80)
                            query = query[..80];
                        writer.WriteString("query", query);
                        continue;
                    }
                }

                writer.WriteEndObject();
            }

            using var clean = JsonDocument.Parse(stream.ToArray());
            return clean.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Empty();
        }
    }

    /// <summary>
    /// Appends an English period phrase when the model selected an allowlisted period.
    /// Existing date words in the user message still win inside <c>ResolveDateRange</c>.
    /// </summary>
    public static string ApplyAllowlistedPeriod(string message, JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("period", out var periodNode)
            || periodNode.ValueKind != JsonValueKind.String)
        {
            return message;
        }

        var phrase = periodNode.GetString() switch
        {
            "today" => "today",
            "yesterday" => "yesterday",
            "this_week" => "this week",
            "last_week" => "last week",
            "this_month" => "this month",
            "last_month" => "last month",
            "last_3_months" => "last 3 months",
            "this_year" => "this year",
            "last_year" => "last year",
            _ => null
        };

        return phrase is null ? message : $"{message} {phrase}";
    }

    public static string? ReadQuery(JsonElement? arguments)
    {
        if (arguments is not { ValueKind: JsonValueKind.Object } element
            || !element.TryGetProperty("query", out var queryNode)
            || queryNode.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var query = queryNode.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(query) ? null : query;
    }

    public static bool IsSecretOrSqlRequest(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var text = message.ToLowerInvariant();
        if (text.Contains("drop table", StringComparison.Ordinal)
            || text.Contains("select * from", StringComparison.Ordinal)
            || text.Contains("union select", StringComparison.Ordinal)
            || text.Contains("; delete", StringComparison.Ordinal)
            || text.Contains("'; drop", StringComparison.Ordinal))
        {
            return true;
        }

        var asksForSecret = text.Contains("api key", StringComparison.Ordinal)
            || text.Contains("apikey", StringComparison.Ordinal)
            || text.Contains("system prompt", StringComparison.Ordinal)
            || text.Contains("connection string", StringComparison.Ordinal)
            || text.Contains("database password", StringComparison.Ordinal);
        if (!asksForSecret)
            return false;

        return text.Contains("reveal", StringComparison.Ordinal)
            || text.Contains("show", StringComparison.Ordinal)
            || text.Contains("print", StringComparison.Ordinal)
            || text.Contains("dump", StringComparison.Ordinal)
            || text.Contains("what is", StringComparison.Ordinal)
            || text.Contains("give me", StringComparison.Ordinal);
    }

    public static bool LooksLikeSql(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.ToLowerInvariant();
        return text.Contains("select ", StringComparison.Ordinal)
            || text.Contains("drop ", StringComparison.Ordinal)
            || text.Contains("insert ", StringComparison.Ordinal)
            || text.Contains("update ", StringComparison.Ordinal)
            || text.Contains("delete ", StringComparison.Ordinal)
            || text.Contains("union ", StringComparison.Ordinal)
            || text.Contains("--", StringComparison.Ordinal)
            || text.Contains(';', StringComparison.Ordinal);
    }

    private static bool IsBlockedProperty(string name) =>
        name.Equals("tenantId", StringComparison.OrdinalIgnoreCase)
        || name.Equals("tenant_id", StringComparison.OrdinalIgnoreCase)
        || name.Equals("tenant", StringComparison.OrdinalIgnoreCase)
        || name.Equals("sql", StringComparison.OrdinalIgnoreCase)
        || name.Equals("connectionString", StringComparison.OrdinalIgnoreCase)
        || name.Equals("apiKey", StringComparison.OrdinalIgnoreCase)
        || name.Equals("api_key", StringComparison.OrdinalIgnoreCase);

    private static JsonElement Empty()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}

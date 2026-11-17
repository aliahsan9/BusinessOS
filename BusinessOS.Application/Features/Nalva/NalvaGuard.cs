using System.Text.Json;
using System.Text.RegularExpressions;

namespace BusinessOS.Application.Features.Nalva;

public static partial class NalvaGuard
{
    private static readonly HashSet<string> BannedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenantid", "tenant", "systemprompt", "grantpermission", "apikey", "api_key",
        "connectionstring", "sql", "query", "ignorepreviousinstructions", "role", "password", "secret"
    };

    public static bool RejectsArguments(string? argsJson, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(argsJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(argsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                reason = "Tool arguments must be a JSON object.";
                return true;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var normalized = property.Name.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
                if (BannedProperties.Contains(normalized))
                {
                    reason = "The request included a field the workforce does not accept.";
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            reason = "Tool arguments were not valid JSON.";
            return true;
        }

        return false;
    }

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return SecretPattern().Replace(value, "$1[redacted]");
    }

    public static int EstimateTokens(string? value) =>
        string.IsNullOrEmpty(value) ? 0 : Math.Max(1, value.Length / 4);

    public static string Clip(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    [GeneratedRegex(@"(?i)(api[_-]?key|password|secret|connectionstring|authorization)(\s*[:=]\s*)([^\s,;}{]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();
}

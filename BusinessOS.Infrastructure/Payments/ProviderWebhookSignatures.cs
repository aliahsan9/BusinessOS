using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BusinessOS.Infrastructure.Payments;

public static class ProviderWebhookSignatures
{
    public static bool TapMatches(string payload, string? hashHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(hashHeader) || string.IsNullOrWhiteSpace(secret))
            return false;

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var hashSource = string.Concat(
                "x_id", Read(root, "id"),
                "x_amount", Read(root, "amount"),
                "x_currency", Read(root, "currency"),
                "x_gateway_reference", Read(root, "reference", "gateway"),
                "x_payment_reference", Read(root, "reference", "payment"),
                "x_status", Read(root, "status"),
                "x_created", Read(root, "transaction", "created"));

            var computed = HmacHex(secret, hashSource);
            return FixedEquals(computed, hashHeader.Trim());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool PayTabsMatches(string payload, string? signatureHeader, string serverKey)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(serverKey))
            return false;

        var computed = HmacHex(serverKey, payload);
        return FixedEquals(computed, signatureHeader.Trim());
    }

    private static string Read(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) ? ValueText(value) : string.Empty;

    private static string Read(JsonElement root, string parent, string property)
    {
        if (!root.TryGetProperty(parent, out var container) || container.ValueKind != JsonValueKind.Object)
            return string.Empty;
        return container.TryGetProperty(property, out var value) ? ValueText(value) : string.Empty;
    }

    private static string ValueText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        _ => value.GetRawText()
    };

    private static string HmacHex(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left.ToLowerInvariant());
        var b = Encoding.UTF8.GetBytes(right.ToLowerInvariant());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}

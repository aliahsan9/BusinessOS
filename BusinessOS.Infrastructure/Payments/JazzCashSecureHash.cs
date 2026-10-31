using System.Security.Cryptography;
using System.Text;

namespace BusinessOS.Infrastructure.Payments;

/// <summary>
/// JazzCash merchant secure-hash construction: HMAC-SHA256 over the integrity salt
/// and the non-empty field values sorted by parameter name, excluding pp_SecureHash.
/// Payment state is still not changed from the callback alone.
/// </summary>
public static class JazzCashSecureHash
{
    public static string Compute(IEnumerable<KeyValuePair<string, string>> fields, string integritySalt)
    {
        var values = fields
            .Where(pair => !pair.Key.Equals("pp_SecureHash", StringComparison.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value);

        var payload = integritySalt + "&" + string.Join("&", values);
        var key = Encoding.UTF8.GetBytes(integritySalt);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Matches(IReadOnlyDictionary<string, string> fields, string integritySalt, string presentedHash)
    {
        if (string.IsNullOrWhiteSpace(integritySalt) || string.IsNullOrWhiteSpace(presentedHash))
        {
            return false;
        }

        var expected = Compute(fields, integritySalt);
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(presentedHash.Trim().ToLowerInvariant());
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

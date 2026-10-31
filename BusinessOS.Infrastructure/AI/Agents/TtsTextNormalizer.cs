using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BusinessOS.Infrastructure.AI.Agents;

/// <summary>Prepares spoken text and stable cache keys.</summary>
public static class TtsTextNormalizer
{
    public const int MaxCharacters = 4096;

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var s = text;
        s = Regex.Replace(s, @"\*\*|__|`+|#+", " ");
        s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]+\)", "$1");
        s = Regex.Replace(s, @"https?://\S+", " ");
        s = Regex.Replace(s, @"\r\n?|\n", ". ");
        s = Regex.Replace(s, @"\s{2,}", " ");
        s = s.Trim();

        if (s.Length > MaxCharacters)
            s = s[..MaxCharacters].TrimEnd() + "…";

        return s;
    }

    public static double ClampRate(double rate)
    {
        if (double.IsNaN(rate) || double.IsInfinity(rate))
            return 1;

        return Math.Round(Math.Clamp(rate, 0.25, 4), 2);
    }

    public static string CacheKey(string provider, string model, string voice, double rate, string text)
    {
        var payload = string.Join(
            '\n',
            provider,
            model,
            voice,
            rate.ToString("0.00", CultureInfo.InvariantCulture),
            text);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

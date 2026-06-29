using System.Text.RegularExpressions;

namespace BusinessOS.Infrastructure.MultiTenancy;

public static partial class SlugHelper
{
    public static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Guid.NewGuid().ToString("N")[..8];
        }

        var slug = name.Trim().ToLowerInvariant();
        slug = NonAlphanumericRegex().Replace(slug, "-");
        slug = MultiDashRegex().Replace(slug, "-").Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = Guid.NewGuid().ToString("N")[..8];
        }

        return slug.Length > 90 ? slug[..90].TrimEnd('-') : slug;
    }

    public static string EnsureUniqueSlug(string baseSlug, Func<string, bool> exists)
    {
        if (!exists(baseSlug))
        {
            return baseSlug;
        }

        for (var i = 2; i <= 100; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..100];
    }

    [GeneratedRegex(@"[^a-z0-9\-]", RegexOptions.Compiled)]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"-+", RegexOptions.Compiled)]
    private static partial Regex MultiDashRegex();
}

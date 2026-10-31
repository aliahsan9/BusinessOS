namespace BusinessOS.API.Versioning;

/// <summary>
/// Shared path rules for URL-segment API versioning.
/// </summary>
public static class ApiVersioningPaths
{
    public const string SupportedVersionsHeader = "api-supported-versions";
    public const string DeprecatedVersionsHeader = "api-deprecated-versions";
    public const string CurrentVersion = "1.0";
    public const string RoutePrefix = "/api/v{version:apiVersion}";
    public const string VersionRequiredCode = "API_VERSION_REQUIRED";
    public const string VersionUnsupportedCode = "API_VERSION_UNSUPPORTED";

    public static bool IsUnversionedBusinessPath(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrEmpty(value) ||
            !value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !HasVersionSegment(value);
    }

    public static bool HasVersionSegment(string path)
    {
        const string prefix = "/api/";
        if (path.Length <= prefix.Length ||
            !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = path.AsSpan(prefix.Length);
        var slash = remainder.IndexOf('/');
        var segment = slash >= 0 ? remainder[..slash] : remainder;
        return IsVersionToken(segment);
    }

    /// <summary>
    /// Turns <c>/api/v1/auth/login</c> into <c>/api/auth/login</c> so public-path checks stay version-independent.
    /// </summary>
    public static string StripVersion(string path)
    {
        if (!HasVersionSegment(path))
        {
            return path;
        }

        var slash = path.IndexOf('/', "/api/".Length);
        return slash < 0 ? "/api" : "/api" + path[slash..];
    }

    public static bool TryGetRequestedVersion(string path, out string version)
    {
        version = string.Empty;
        if (!HasVersionSegment(path))
        {
            return false;
        }

        var remainder = path.AsSpan("/api/".Length);
        var slash = remainder.IndexOf('/');
        var segment = slash >= 0 ? remainder[..slash] : remainder;
        var raw = segment[1..].ToString();
        version = raw.Contains('.', StringComparison.Ordinal) ? raw : raw + ".0";
        return true;
    }

    public static bool IsSupportedVersion(string version) =>
        version.Equals(CurrentVersion, StringComparison.Ordinal);

    private static bool IsVersionToken(ReadOnlySpan<char> segment)
    {
        if (segment.Length < 2 || segment[0] is not ('v' or 'V'))
        {
            return false;
        }

        var digits = segment[1..];
        if (digits.IsEmpty || !char.IsDigit(digits[0]) || !char.IsDigit(digits[^1]))
        {
            return false;
        }

        var dotSeen = false;
        foreach (var character in digits)
        {
            if (character == '.')
            {
                if (dotSeen)
                {
                    return false;
                }

                dotSeen = true;
                continue;
            }

            if (!char.IsDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}

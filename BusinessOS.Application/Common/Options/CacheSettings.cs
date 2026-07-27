namespace BusinessOS.Application.Common.Options;

/// <summary>
/// In-memory cache expiration settings loaded via the Options pattern.
/// </summary>
public sealed class CacheSettings
{
    public const string SectionName = "CacheSettings";

    /// <summary>Default absolute expiration for general query results (minutes).</summary>
    public int DefaultExpirationMinutes { get; set; } = 5;

    /// <summary>Absolute expiration for dashboard aggregates (minutes).</summary>
    public int DashboardExpirationMinutes { get; set; } = 1;

    /// <summary>Absolute expiration for report query results (minutes).</summary>
    public int ReportExpirationMinutes { get; set; } = 2;

    /// <summary>Absolute expiration for static lookup tables such as categories (minutes).</summary>
    public int StaticDataExpirationMinutes { get; set; } = 30;

    public TimeSpan DefaultExpiration => TimeSpan.FromMinutes(Math.Max(1, DefaultExpirationMinutes));

    public TimeSpan DashboardExpiration => TimeSpan.FromMinutes(Math.Max(1, DashboardExpirationMinutes));

    public TimeSpan ReportExpiration => TimeSpan.FromMinutes(Math.Max(1, ReportExpirationMinutes));

    public TimeSpan StaticDataExpiration => TimeSpan.FromMinutes(Math.Max(1, StaticDataExpirationMinutes));
}

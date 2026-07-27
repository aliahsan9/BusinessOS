namespace BusinessOS.Application.Common.Options;

public sealed class LoggingPerformanceOptions
{
    public const string SectionName = "Logging:Performance";

    /// <summary>Warn when a MediatR handler exceeds this duration (ms).</summary>
    public int MediatRWarningThresholdMs { get; set; } = 2000;

    /// <summary>Warn when an HTTP request exceeds this duration (ms).</summary>
    public int HttpWarningThresholdMs { get; set; } = 3000;

    /// <summary>Warn when a database command exceeds this duration (ms).</summary>
    public int SlowQueryThresholdMs { get; set; } = 500;

    /// <summary>Warn when an AI operation exceeds this duration (ms).</summary>
    public int AiWarningThresholdMs { get; set; } = 15000;
}

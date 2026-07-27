using System.Data.Common;
using BusinessOS.Application.Common.Options;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Diagnostics;

/// <summary>
/// Logs slow database commands without capturing full SQL in production-style noise.
/// Command text is only included at Debug level.
/// </summary>
public sealed class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly ILogger<SlowQueryInterceptor> _logger;
    private readonly IOptionsMonitor<LoggingPerformanceOptions> _options;

    public SlowQueryInterceptor(
        ILogger<SlowQueryInterceptor> logger,
        IOptionsMonitor<LoggingPerformanceOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogIfSlow(command, eventData.Duration);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData.Duration);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogIfSlow(command, eventData.Duration);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData.Duration);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        LogIfSlow(command, eventData.Duration);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData.Duration);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        _logger.LogError(
            eventData.Exception,
            "Database command failed after {ElapsedMilliseconds}ms ({CommandType})",
            eventData.Duration.TotalMilliseconds,
            command.CommandType);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _logger.LogError(
            eventData.Exception,
            "Database command failed after {ElapsedMilliseconds}ms ({CommandType})",
            eventData.Duration.TotalMilliseconds,
            command.CommandType);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void LogIfSlow(DbCommand command, TimeSpan duration)
    {
        var thresholdMs = _options.CurrentValue.SlowQueryThresholdMs;
        if (thresholdMs <= 0 || duration.TotalMilliseconds < thresholdMs)
        {
            return;
        }

        _logger.LogWarning(
            "Slow database query detected: {ElapsedMilliseconds}ms (threshold {ThresholdMs}ms, CommandType={CommandType})",
            (long)duration.TotalMilliseconds,
            thresholdMs,
            command.CommandType);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Slow query command text: {CommandText}",
                command.CommandText);
        }
    }
}

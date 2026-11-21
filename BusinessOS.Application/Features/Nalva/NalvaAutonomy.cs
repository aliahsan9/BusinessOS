using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BusinessOS.Application.Features.Nalva;

public static class NalvaEventNames
{
    public const string CustomerRegistered = "CustomerRegistered";
    public const string LeadCreated = "LeadCreated";
    public const string LeadQualified = "LeadQualified";
    public const string OrderPlaced = "OrderPlaced";
    public const string PaymentReceived = "PaymentReceived";
    public const string InvoiceOverdue = "InvoiceOverdue";
    public const string StockBelowThreshold = "StockBelowThreshold";
    public const string DeliveryDelayed = "DeliveryDelayed";
    public const string ProjectCreated = "ProjectCreated";
    public const string TaskCompleted = "TaskCompleted";
    public const string TaskOverdue = "TaskOverdue";
    public const string EmployeeOnboarded = "EmployeeOnboarded";
    public const string SupportTicketCreated = "SupportTicketCreated";

    public static readonly string[] Known =
    [
        CustomerRegistered, LeadCreated, LeadQualified, OrderPlaced, PaymentReceived, InvoiceOverdue,
        StockBelowThreshold, DeliveryDelayed, ProjectCreated, TaskCompleted, TaskOverdue, EmployeeOnboarded, SupportTicketCreated
    ];

    public static bool IsAllowed(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
            return false;
        if (Known.Contains(name, StringComparer.Ordinal))
            return true;
        if (!name.StartsWith("company.", StringComparison.Ordinal) || name.Length < 9)
            return false;
        return name.All(character => char.IsLetterOrDigit(character) || character is '.' or '_');
    }
}

public static class NalvaExecutionAmbient
{
    private static readonly AsyncLocal<int> DepthLocal = new();

    public static int Depth => DepthLocal.Value;

    public static IDisposable Push()
    {
        var previous = DepthLocal.Value;
        DepthLocal.Value = previous + 1;
        return new Popper(previous);
    }

    private sealed class Popper(int previous) : IDisposable
    {
        public void Dispose() => DepthLocal.Value = previous;
    }
}

public sealed class NalvaWorkflowRunOptions
{
    public bool DryRun { get; init; }
    public bool RequireActive { get; init; } = true;
    public string TriggerSource { get; init; } = "Manual";
    public string? CorrelationId { get; init; }
    public bool Resume { get; init; }
}

public static class NalvaPlanPolicy
{
    public static IReadOnlyList<NalvaPlannedTool> Accept(NalvaAgentDefinition agent, IEnumerable<string> proposedTools, string argsJson)
    {
        var allowed = agent.Tools.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var safeArgs = string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson;
        return proposedTools
            .Where(tool => !string.IsNullOrWhiteSpace(tool))
            .Select(tool => tool.Trim())
            .Where(tool => allowed.Contains(tool) && NalvaToolCatalog.TryGet(tool, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(NalvaLimits.MaxToolCalls)
            .Select(tool =>
            {
                var definition = NalvaToolCatalog.Get(tool);
                return new NalvaPlannedTool(definition.Name, safeArgs, 1, definition.RequiresApproval, null);
            })
            .ToArray();
    }

    public static IReadOnlyList<string> ReadToolNames(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return [];
        var start = reply.IndexOf('[');
        var end = reply.LastIndexOf(']');
        if (start < 0 || end <= start)
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(reply[start..(end + 1)]) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public static class NalvaFailureClassifier
{
    public static bool IsRetryable(string? errorClass) =>
        errorClass is NalvaErrorClass.Transient or NalvaErrorClass.RateLimit or NalvaErrorClass.Timeout;

    public static string ClassifyMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return NalvaErrorClass.Permanent;
        var text = message.ToLowerInvariant();
        if (text.Contains("rate limit") || text.Contains("429"))
            return NalvaErrorClass.RateLimit;
        if (text.Contains("timeout") || text.Contains("timed out"))
            return NalvaErrorClass.Timeout;
        if (text.Contains("permission") || text.Contains("forbidden"))
            return NalvaErrorClass.PermissionDenied;
        if (text.Contains("not configured") || text.Contains("not connected"))
            return NalvaErrorClass.Integration;
        return NalvaErrorClass.Permanent;
    }
}

public static class NalvaRecurrenceKinds
{
    public const string Once = "Once";
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";
    public const string Custom = "Custom";
    public const string Interval = "Interval";

    public static readonly string[] All = [Once, Daily, Weekly, Monthly, Custom, Interval];

    public static bool IsKnown(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && All.Contains(kind, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? kind) =>
        IsKnown(kind) ? All.First(x => x.Equals(kind, StringComparison.OrdinalIgnoreCase)) : Interval;
}

public readonly record struct NalvaScheduleSpec(
    string? TimeZoneId,
    string RecurrenceKind,
    int? IntervalMinutes,
    DateTime? StartUtc,
    DateTime? EndUtc,
    string? LocalTimeOfDay,
    string? DaysOfWeek,
    int? DayOfMonth);

public static class NalvaSchedule
{
    public static bool TryWindow(
        DateTime utcNow,
        string? timeZoneId,
        int? intervalMinutes,
        DateTime? startUtc,
        DateTime? endUtc,
        string? lastWindow,
        out string windowKey,
        out DateTime nextUtc) =>
        TryDue(new NalvaScheduleSpec(timeZoneId, NalvaRecurrenceKinds.Interval, intervalMinutes, startUtc, endUtc, null, null, null), utcNow, lastWindow, out windowKey, out nextUtc);

    public static bool TryDue(
        NalvaScheduleSpec spec,
        DateTime utcNow,
        string? lastWindow,
        out string windowKey,
        out DateTime nextUtc)
    {
        windowKey = "";
        nextUtc = utcNow;
        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        if (spec.StartUtc is DateTime start && now < DateTime.SpecifyKind(start, DateTimeKind.Utc))
        {
            nextUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            return false;
        }

        if (spec.EndUtc is DateTime end && now > DateTime.SpecifyKind(end, DateTimeKind.Utc))
        {
            nextUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc).AddMinutes(5);
            return false;
        }

        var kind = NalvaRecurrenceKinds.Normalize(spec.RecurrenceKind);
        if (kind == NalvaRecurrenceKinds.Once || (kind == NalvaRecurrenceKinds.Interval && spec.IntervalMinutes is null or <= 0))
        {
            windowKey = "once";
            nextUtc = now.AddYears(100);
            return !string.Equals(lastWindow, windowKey, StringComparison.Ordinal);
        }

        if (kind == NalvaRecurrenceKinds.Interval)
        {
            var interval = Math.Clamp(spec.IntervalMinutes ?? 60, 5, 43_200);
            windowKey = WindowKey(now, spec.TimeZoneId, interval);
            nextUtc = now.AddMinutes(interval);
            return !string.Equals(lastWindow, windowKey, StringComparison.Ordinal);
        }

        var local = Convert(now, spec.TimeZoneId);
        var time = ParseLocalTime(spec.LocalTimeOfDay);
        var localSlot = new DateTime(local.Year, local.Month, local.Day, time.Hours, time.Minutes, 0, DateTimeKind.Unspecified);

        return kind switch
        {
            NalvaRecurrenceKinds.Daily => TryCalendarWindow(local, localSlot, lastWindow, "d", TimeSpan.FromDays(1), spec.TimeZoneId, out windowKey, out nextUtc),
            NalvaRecurrenceKinds.Weekly => TryWeekly(local, localSlot, spec.DaysOfWeek, lastWindow, spec.TimeZoneId, out windowKey, out nextUtc),
            NalvaRecurrenceKinds.Monthly => TryMonthly(local, time, spec.DayOfMonth, lastWindow, spec.TimeZoneId, out windowKey, out nextUtc),
            NalvaRecurrenceKinds.Custom => TryWeekly(local, localSlot, spec.DaysOfWeek, lastWindow, spec.TimeZoneId, out windowKey, out nextUtc),
            _ => TryCalendarWindow(local, localSlot, lastWindow, "d", TimeSpan.FromDays(1), spec.TimeZoneId, out windowKey, out nextUtc)
        };
    }

    private static bool TryWeekly(
        DateTime local,
        DateTime localSlot,
        string? daysOfWeek,
        string? lastWindow,
        string? timeZoneId,
        out string windowKey,
        out DateTime nextUtc)
    {
        var allowed = ParseDaysOfWeek(daysOfWeek);
        if (allowed.Count == 0)
            allowed = [0, 1, 2, 3, 4, 5, 6];

        for (var offset = 0; offset < 14; offset++)
        {
            var candidateLocal = localSlot.AddDays(offset);
            if (candidateLocal < local && offset == 0)
                continue;
            if (!allowed.Contains((int)candidateLocal.DayOfWeek))
                continue;

            windowKey = $"w:{candidateLocal:yyyy-MM-dd}";
            nextUtc = ToUtc(candidateLocal.AddDays(7), timeZoneId);
            if (candidateLocal > local)
            {
                nextUtc = ToUtc(candidateLocal, timeZoneId);
                return false;
            }

            return !string.Equals(lastWindow, windowKey, StringComparison.Ordinal);
        }

        windowKey = "";
        nextUtc = ToUtc(local.AddDays(1), timeZoneId);
        return false;
    }

    private static bool TryMonthly(
        DateTime local,
        TimeSpan time,
        int? dayOfMonth,
        string? lastWindow,
        string? timeZoneId,
        out string windowKey,
        out DateTime nextUtc)
    {
        var day = Math.Clamp(dayOfMonth ?? local.Day, 1, 28);
        var thisMonth = SafeLocal(local.Year, local.Month, day, time);
        if (thisMonth <= local)
        {
            windowKey = $"m:{thisMonth:yyyy-MM}";
            var nextMonth = thisMonth.AddMonths(1);
            nextUtc = ToUtc(SafeLocal(nextMonth.Year, nextMonth.Month, day, time), timeZoneId);
            return !string.Equals(lastWindow, windowKey, StringComparison.Ordinal);
        }

        windowKey = $"m:{thisMonth:yyyy-MM}";
        nextUtc = ToUtc(thisMonth, timeZoneId);
        return false;
    }

    private static bool TryCalendarWindow(
        DateTime local,
        DateTime localSlot,
        string? lastWindow,
        string prefix,
        TimeSpan period,
        string? timeZoneId,
        out string windowKey,
        out DateTime nextUtc)
    {
        if (local < localSlot)
        {
            windowKey = $"{prefix}:{localSlot:yyyy-MM-dd}";
            nextUtc = ToUtc(localSlot, timeZoneId);
            return false;
        }

        windowKey = $"{prefix}:{localSlot:yyyy-MM-dd}";
        nextUtc = ToUtc(localSlot.Add(period), timeZoneId);
        return !string.Equals(lastWindow, windowKey, StringComparison.Ordinal);
    }

    public static string WindowKey(DateTime utcNow, string? timeZoneId, int intervalMinutes)
    {
        var local = Convert(utcNow, timeZoneId);
        var interval = Math.Clamp(intervalMinutes, 5, 43_200);
        if (interval >= 1440)
        {
            var days = Math.Max(1, interval / 1440);
            var index = local.DayOfYear / days;
            return $"{local.Year}-d{index}";
        }

        if (interval >= 60)
        {
            var hours = Math.Max(1, interval / 60);
            var bucket = local.Hour / hours * hours;
            return $"{local:yyyy-MM-dd}T{bucket:00}";
        }

        var minute = local.Minute / interval * interval;
        return $"{local:yyyy-MM-dd}T{local.Hour:00}:{minute:00}";
    }

    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
            return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static TimeSpan ParseLocalTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new TimeSpan(9, 0, 0);
        if (TimeSpan.TryParseExact(value.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var parsed)
            || TimeSpan.TryParseExact(value.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out parsed)
            || TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out parsed))
        {
            return new TimeSpan(parsed.Hours, parsed.Minutes, 0);
        }

        return new TimeSpan(9, 0, 0);
    }

    public static HashSet<int> ParseDaysOfWeek(string? value)
    {
        var result = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(value))
            return result;
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var number) && number is >= 0 and <= 6)
            {
                result.Add(number);
                continue;
            }

            var mapped = part.ToLowerInvariant() switch
            {
                "sun" or "sunday" => 0,
                "mon" or "monday" => 1,
                "tue" or "tues" or "tuesday" => 2,
                "wed" or "wednesday" => 3,
                "thu" or "thur" or "thursday" => 4,
                "fri" or "friday" => 5,
                "sat" or "saturday" => 6,
                _ => -1
            };
            if (mapped >= 0)
                result.Add(mapped);
        }

        return result;
    }

    private static DateTime Convert(DateTime utcNow, string? timeZoneId)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Resolve(timeZoneId));
    }

    private static DateTime ToUtc(DateTime localUnspecified, string? timeZoneId)
    {
        var tz = Resolve(timeZoneId);
        var local = DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    private static DateTime SafeLocal(int year, int month, int day, TimeSpan time)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var safeDay = Math.Min(day, daysInMonth);
        return new DateTime(year, month, safeDay, time.Hours, time.Minutes, 0, DateTimeKind.Unspecified);
    }
}

/// <summary>Prevents the same automation correlation from retriggering endlessly within a short window.</summary>
public static class NalvaAutomationLoopGuard
{
    public const int MaxSameCorrelationPerHour = 3;

    public static string CorrelationFingerprint(string? correlationId, Guid definitionId) =>
        $"{definitionId:N}:{(correlationId ?? "").Trim()}";
}

public static class NalvaConditionEvaluator
{
    public static bool TryEvaluate(string? expression, JsonObject context, out bool result, out string? error)
    {
        result = true;
        error = null;
        if (string.IsNullOrWhiteSpace(expression))
            return true;
        try
        {
            var parser = new Parser(expression, context);
            result = parser.ParseOr();
            if (!parser.Finished)
            {
                error = "The condition has unsupported syntax.";
                return false;
            }
            return true;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private sealed class Parser(string expression, JsonObject context)
    {
        private int _index;
        private int _depth;

        public bool Finished => Skip() && _index >= expression.Length;

        public bool ParseOr()
        {
            var value = ParseAnd();
            while (Match("||"))
                value = value || ParseAnd();
            return value;
        }

        private bool ParseAnd()
        {
            var value = ParseComparison();
            while (Match("&&"))
                value = value && ParseComparison();
            return value;
        }

        private bool ParseComparison()
        {
            if (_depth > NalvaLimits.MaxEventDepth + 1)
                throw new InvalidOperationException("The condition is nested too deeply.");
            Skip();
            if (Match("("))
            {
                _depth++;
                var grouped = ParseOr();
                if (!Match(")"))
                    throw new InvalidOperationException("The condition is missing a closing parenthesis.");
                _depth--;
                return grouped;
            }

            var left = ReadValue();
            Skip();
            var op = ReadOperator();
            var right = ReadValue();
            return Compare(left, op, right);
        }

        private bool Match(string token)
        {
            Skip();
            if (!_expressionStarts(token))
                return false;
            _index += token.Length;
            return true;
        }

        private bool _expressionStarts(string token) =>
            _index + token.Length <= expression.Length
            && string.Compare(expression, _index, token, 0, token.Length, StringComparison.Ordinal) == 0;

        private bool Skip()
        {
            while (_index < expression.Length && char.IsWhiteSpace(expression[_index]))
                _index++;
            return true;
        }

        private string ReadOperator()
        {
            foreach (var op in new[] { "==", "!=", ">=", "<=", ">", "<" })
            {
                if (Match(op))
                    return op;
            }
            throw new InvalidOperationException("The condition is missing a comparison.");
        }

        private JsonNode? ReadValue()
        {
            Skip();
            if (_index >= expression.Length)
                throw new InvalidOperationException("The condition is incomplete.");
            var current = expression[_index];
            if (current is '"' or '\'')
            {
                var quote = current;
                _index++;
                var start = _index;
                while (_index < expression.Length && expression[_index] != quote)
                    _index++;
                if (_index >= expression.Length)
                    throw new InvalidOperationException("The condition has an unterminated string.");
                var text = expression[start.._index];
                _index++;
                return JsonValue.Create(text);
            }

            var tokenStart = _index;
            while (_index < expression.Length && (char.IsLetterOrDigit(expression[_index]) || expression[_index] is '.' or '_' or '-'))
                _index++;
            var token = expression[tokenStart.._index];
            if (token.Length == 0)
                throw new InvalidOperationException("The condition has an unsupported value.");
            if (token.Equals("true", StringComparison.OrdinalIgnoreCase))
                return JsonValue.Create(true);
            if (token.Equals("false", StringComparison.OrdinalIgnoreCase))
                return JsonValue.Create(false);
            if (token.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;
            if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                return JsonValue.Create(number);
            if (token.Contains("..", StringComparison.Ordinal) || token.StartsWith('.') || token.StartsWith("__", StringComparison.Ordinal))
                throw new InvalidOperationException("The condition references an unsupported property.");
            var resolved = Resolve(token, out var found);
            return found ? resolved : JsonValue.Create("\u0000" + token);
        }

        private JsonNode? Resolve(string path, out bool found)
        {
            found = true;
            JsonNode? current = context;
            foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current is not JsonObject obj || !obj.TryGetPropertyValue(part, out current))
                {
                    found = false;
                    return null;
                }
            }
            return current;
        }

        private static bool Compare(JsonNode? left, string op, JsonNode? right)
        {
            if (left is null || right is null)
            {
                var bothNull = left is null && right is null;
                return op switch
                {
                    "==" => bothNull,
                    "!=" => !bothNull,
                    _ => false
                };
            }

            if (TryNumber(left, out var leftNumber) && TryNumber(right, out var rightNumber))
            {
                return op switch
                {
                    "==" => leftNumber == rightNumber,
                    "!=" => leftNumber != rightNumber,
                    ">" => leftNumber > rightNumber,
                    ">=" => leftNumber >= rightNumber,
                    "<" => leftNumber < rightNumber,
                    "<=" => leftNumber <= rightNumber,
                    _ => false
                };
            }

            var leftText = Text(left);
            var rightText = Text(right);
            var equal = string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);
            return op switch
            {
                "==" => equal,
                "!=" => !equal,
                _ => false
            };
        }

        private static bool TryNumber(JsonNode node, out decimal number)
        {
            number = 0;
            if (node is JsonValue value && value.TryGetValue<decimal>(out number))
                return true;
            return decimal.TryParse(Text(node), NumberStyles.Number, CultureInfo.InvariantCulture, out number);
        }

        private static string Text(JsonNode node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node.ToString();
    }
}

public static class NalvaWorkflowGraph
{
    public static IReadOnlyList<IReadOnlyList<NalvaWorkflowStepWrite>> Group(IReadOnlyList<NalvaWorkflowStepWrite> steps)
    {
        var groups = new List<List<NalvaWorkflowStepWrite>>();
        foreach (var step in steps)
        {
            var canJoin = step.ParallelGroup is > 0
                && IsReadOnly(step)
                && groups.Count > 0
                && groups[^1].Count < NalvaLimits.MaxParallelSteps
                && groups[^1][0].ParallelGroup == step.ParallelGroup
                && groups[^1].All(IsReadOnly);
            if (canJoin)
                groups[^1].Add(step);
            else
                groups.Add([step]);
        }
        return groups;
    }

    public static bool IsReadOnly(NalvaWorkflowStepWrite step) =>
        step.Tools.Count > 0
        && step.Tools.All(tool =>
            NalvaToolCatalog.TryGet(tool, out var definition)
            && definition is not null
            && !definition.IsWrite
            && !definition.ExternalEffect
            && !definition.RequiresApproval);
}

public interface INalvaEventOutbox
{
    Task<Guid?> EnqueueAsync(string name, string payloadJson, string? idempotencyKey, CancellationToken cancellationToken = default);
}

public interface INalvaEventDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default);
    Task<int> DispatchOneAsync(Guid eventId, CancellationToken cancellationToken = default);
}

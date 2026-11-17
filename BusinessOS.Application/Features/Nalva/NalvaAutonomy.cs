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
    public const string ProjectCreated = "ProjectCreated";
    public const string TaskCompleted = "TaskCompleted";
    public const string TaskOverdue = "TaskOverdue";
    public const string EmployeeOnboarded = "EmployeeOnboarded";
    public const string SupportTicketCreated = "SupportTicketCreated";

    public static readonly string[] Known =
    [
        CustomerRegistered, LeadCreated, LeadQualified, OrderPlaced, PaymentReceived, InvoiceOverdue,
        StockBelowThreshold, ProjectCreated, TaskCompleted, TaskOverdue, EmployeeOnboarded, SupportTicketCreated
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
        out DateTime nextUtc)
    {
        windowKey = "";
        nextUtc = utcNow;
        var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        if (startUtc is DateTime start && now < DateTime.SpecifyKind(start, DateTimeKind.Utc))
            return false;
        if (endUtc is DateTime end && now > DateTime.SpecifyKind(end, DateTimeKind.Utc))
            return false;

        if (intervalMinutes is null or <= 0)
        {
            windowKey = "once";
            nextUtc = now;
            return !string.Equals(lastWindow, windowKey, StringComparison.Ordinal);
        }

        var interval = Math.Clamp(intervalMinutes.Value, 5, 43_200);
        windowKey = WindowKey(now, timeZoneId, interval);
        nextUtc = now.AddMinutes(interval);
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

    private static DateTime Convert(DateTime utcNow, string? timeZoneId)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Resolve(timeZoneId));
    }
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

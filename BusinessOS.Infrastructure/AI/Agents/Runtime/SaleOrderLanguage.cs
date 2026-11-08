using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessOS.Application.Features.Orders.Constants;

namespace BusinessOS.Infrastructure.AI.Agents.Runtime;

/// <summary>
/// Reads customer, product, and quantity from a sale message without adding them together
/// or treating model numbers as a quantity.
/// </summary>
public static partial class SaleOrderLanguage
{
    private static readonly HashSet<string> ExactConfirmations = new(StringComparer.OrdinalIgnoreCase)
    {
        "yes", "y", "yeah", "yep", "ok", "okay", "sure", "confirm", "confirmed",
        "go ahead", "do it", "create it", "yes create it", "yes please", "please do",
        "proceed", "thats correct", "correct", "looks good", "go for it",
        "yes do it", "yes go ahead", "yes proceed", "yes please proceed",
        "haan", "han", "ji", "jee", "theek hai", "theek", "kar do",
        "yes delete it", "yes update it", "yes create the order", "yes create the product",
        "yes create the supplier", "yes save it", "do that", "make it",
        "create it now", "actually create it now", "create this customer", "create the customer",
        "create this order", "create the order now"
    };

    private static readonly HashSet<string> ConfirmationFiller = new(StringComparer.OrdinalIgnoreCase)
    {
        "yes", "y", "yeah", "yep", "ok", "okay", "sure", "confirm", "confirmed",
        "go", "ahead", "do", "it", "create", "the", "order", "please", "proceed",
        "correct", "right", "that", "thats", "looks", "good", "for", "save",
        "update", "delete", "haan", "han", "ji", "jee", "theek", "hai", "kar"
    };

    private static readonly HashSet<string> StrongConfirmation = new(StringComparer.OrdinalIgnoreCase)
    {
        "yes", "y", "yeah", "yep", "ok", "okay", "sure", "confirm", "confirmed",
        "proceed", "correct", "haan", "han", "ji", "jee", "theek", "do"
    };

    private static readonly Dictionary<string, decimal> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10
    };

    private static readonly HashSet<string> ProductStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "create", "add", "new", "order", "orders", "sale", "sales", "please", "the", "a", "an",
        "named", "called", "customer", "client", "for", "with", "make", "me", "this", "that",
        "and", "of", "to", "my", "want", "need", "like", "would", "just", "also", "quantity", "qty",
        "name", "is", "him", "her", "his", "each", "price", "rs", "pkr", "cost",
        "dont", "don't", "not", "who", "you", "your", "what", "which", "tell", "can", "do",
        "speak", "switch", "language", "urdu", "english", "arabic", "accent", "hey"
    };

    private static readonly JsonSerializerOptions ChoiceJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static bool IsConfirmation(string message)
    {
        var normalized = Normalize(message);
        if (normalized.Length == 0)
            return false;
        if (ExactConfirmations.Contains(normalized))
            return true;

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is 0 or > 8)
            return false;
        if (tokens.Any(token => !ConfirmationFiller.Contains(token)))
            return false;
        return tokens.Any(token => StrongConfirmation.Contains(token));
    }

    public static void ApplyToSlots(string message, Dictionary<string, string?> slots)
    {
        if (IsConfirmation(message))
            return;

        var customer = ExtractCustomerName(message);
        if (!string.IsNullOrWhiteSpace(customer))
        {
            var existing = Get(slots, "customerName");
            if (!string.IsNullOrWhiteSpace(existing)
                && !existing.Equals(customer, StringComparison.OrdinalIgnoreCase))
            {
                slots.Remove("customerId");
                slots.Remove("customerChoices");
            }

            slots["customerName"] = customer;
            slots["customerQuery"] = customer;
        }

        var product = ExtractProductPhrase(message);
        if (!string.IsNullOrWhiteSpace(product)
            && !product.Equals(customer, StringComparison.OrdinalIgnoreCase))
        {
            var previous = Get(slots, "productQuery");
            if (!string.IsNullOrWhiteSpace(previous)
                && !previous.Equals(product, StringComparison.OrdinalIgnoreCase))
            {
                slots.Remove("productId");
                slots.Remove("productChoices");
            }

            slots["productName"] = product;
            slots["productQuery"] = product;
        }

        if (TryParseQuantity(message, out var quantity))
            slots["quantity"] = quantity.ToString(CultureInfo.InvariantCulture);

        if (TryGetRequestedUnitPrice(message, out var unitPrice))
            slots["unitPrice"] = unitPrice.ToString(CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(Get(slots, "productName"))
            && Regex.IsMatch(message, @"\b(available product|my product|this product)\b", RegexOptions.IgnoreCase))
        {
            slots["productName"] = "available product";
            slots["productQuery"] = "available product";
        }
    }

    public static bool TryParseQuantity(string message, out decimal quantity)
    {
        quantity = 0;
        if (string.IsNullOrWhiteSpace(message) || IsConfirmation(message))
            return false;

        var labeled = LabeledQuantityRegex().Match(message);
        if (labeled.Success && TryDecimal(labeled.Groups[1].Value, out quantity))
            return true;

        var signals = new List<decimal>();
        foreach (Match match in NumberWordRegex().Matches(message))
        {
            if (IsGenerationPrefix(message, match.Index))
                continue;
            if (NumberWords.TryGetValue(match.Value, out var word))
                signals.Add(word);
        }

        foreach (Match match in BareNumberRegex().Matches(message))
        {
            if (IsGenerationPrefix(message, match.Index) || IsPriceNumber(message, match))
                continue;
            if (TryDecimal(match.Value, out var number))
                signals.Add(number);
        }

        var distinct = signals.Distinct().ToList();
        if (distinct.Count != 1)
            return false;

        quantity = distinct[0];
        return quantity > 0 && quantity <= OrderConstants.MaxItemQuantity;
    }

    public static bool TryGetRequestedUnitPrice(string? message, out decimal price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(message))
            return false;

        foreach (Match match in BareNumberRegex().Matches(message))
        {
            if (!IsPriceNumber(message, match) || !TryDecimal(match.Value, out var number))
                continue;
            if (number > price)
                price = number;
        }

        return price > 0;
    }

    public static bool TryGetSlotQuantity(IReadOnlyDictionary<string, string?> slots, out decimal quantity)
    {
        quantity = 0;
        if (!slots.TryGetValue("quantity", out var raw) || string.IsNullOrWhiteSpace(raw))
            return false;
        return TryDecimal(raw, out quantity)
            && quantity > 0
            && quantity <= OrderConstants.MaxItemQuantity;
    }

    public static string? ExtractCustomerName(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || IsConfirmation(message))
            return null;

        var named = NameIsRegex().Match(message);
        if (named.Success)
        {
            var fromLabel = TrimName(named.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(fromLabel))
                return fromLabel;
        }

        var match = CustomerNameRegex().Match(message);
        if (!match.Success)
            return null;

        var name = TrimName(match.Groups[1].Value);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    public static string? ExtractProductPhrase(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || IsConfirmation(message))
            return null;

        var customer = ExtractCustomerName(message);
        var text = message;
        if (!string.IsNullOrWhiteSpace(customer))
        {
            text = Regex.Replace(
                text,
                Regex.Escape(customer),
                " ",
                RegexOptions.IgnoreCase);
        }

        text = LabeledQuantityRegex().Replace(text, " ");
        text = NumberWordRegex().Replace(text, match => IsGenerationPrefix(text, match.Index) ? match.Value : " ");
        text = LeadingCountRegex().Replace(text, " ");
        text = Regex.Replace(text, @"\b(" + string.Join("|", ProductStopWords.Where(word => !word.Contains('\''))) + @")\b", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b\d{3,9}(?:\.\d+)?\b", " ");
        text = Regex.Replace(text, @"[,:;]+", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim(' ', '.', '-', '"', '\'');

        if (text.Length < 2 || IsRejectedProductPhrase(text))
            return null;
        if (!string.IsNullOrWhiteSpace(customer)
            && text.Equals(customer, StringComparison.OrdinalIgnoreCase))
            return null;
        if (text.Equals("order", StringComparison.OrdinalIgnoreCase)
            || text.Equals("sale", StringComparison.OrdinalIgnoreCase))
            return null;

        return text;
    }

    public static bool TrySelectChoice(string message, string? choicesJson, out Guid id, out string label)
    {
        id = Guid.Empty;
        label = "";
        var choices = ReadChoices(choicesJson);
        if (choices.Count == 0 || string.IsNullOrWhiteSpace(message) || IsConfirmation(message))
            return false;

        var trimmed = message.Trim();
        var ordinal = OrdinalIndex(trimmed);
        if (ordinal is int index && index >= 0 && index < choices.Count)
        {
            id = choices[index].Id;
            label = choices[index].Label;
            return true;
        }

        var needle = trimmed.Trim(' ', '"', '\'', '.', ',', ':');
        if (needle.Length < 2)
            return false;

        var matches = choices
            .Where(choice =>
                choice.Label.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || needle.Contains(choice.Label, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        if (matches.Count != 1)
            return false;

        id = matches[0].Id;
        label = matches[0].Label;
        return true;
    }

    public static string WriteChoices(IEnumerable<(Guid Id, string Label)> choices) =>
        JsonSerializer.Serialize(
            choices.Select(choice => new ChoiceRow(choice.Id, choice.Label)).ToList(),
            ChoiceJson);

    public static string NormalizeCatalogText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value.ToLowerInvariant();
        text = Regex.Replace(text, @"\bgen(?:eration)?\b", "generation");
        text = Regex.Replace(text, @"\bg\s*(\d+)\b", "generation $1");
        text = Regex.Replace(text, @"[^a-z0-9]+", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    public static IReadOnlyList<string> CatalogTokens(string phrase)
    {
        var normalized = NormalizeCatalogText(phrase);
        var tokens = new List<string>();
        foreach (var raw in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw is "a" or "an" or "the" or "for" or "of" or "and")
                continue;

            var token = raw.EndsWith('s') && raw.Length > 3 && !char.IsDigit(raw[0])
                ? raw[..^1]
                : raw;
            if (!tokens.Contains(token, StringComparer.Ordinal))
                tokens.Add(token);
        }

        return tokens;
    }

    public static bool ContainsToken(string normalizedHaystack, string token)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(normalizedHaystack))
            return false;
        return Regex.IsMatch(normalizedHaystack, $@"\b{Regex.Escape(token)}\b");
    }

    private static List<ChoiceRow> ReadChoices(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<ChoiceRow>>(json, ChoiceJson)?
                .Where(row => row.Id != Guid.Empty && !string.IsNullOrWhiteSpace(row.Label))
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static int? OrdinalIndex(string message)
    {
        var normalized = Normalize(message);
        if (int.TryParse(normalized, out var number) && number is >= 1 and <= 9)
            return number - 1;

        return normalized switch
        {
            "first" or "the first" or "the first one" or "option 1" or "number 1" => 0,
            "second" or "the second" or "the second one" or "option 2" or "number 2" => 1,
            "third" or "the third" or "the third one" or "option 3" or "number 3" => 2,
            _ => null
        };
    }

    private static bool IsPriceNumber(string message, Match match)
    {
        var start = Math.Max(0, match.Index - 12);
        var end = Math.Min(message.Length, match.Index + match.Length + 14);
        var window = message[start..end];
        return Regex.IsMatch(window, @"\b(price|each|rs|pkr|usd|cost|rate|\$)\b", RegexOptions.IgnoreCase);
    }

    private static bool IsRejectedProductPhrase(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (normalized is "who" or "you" or "are" or "language" or "urdu" or "english" or "arabic"
            or "accent" or "switch" or "speak" or "top" or "sold" or "product" or "dont" or "cancel" or "change")
            return true;

        return Regex.IsMatch(
            normalized,
            @"\b(who are|what can|what do you|switch|speak|accent|language|ziada|zyada|kon sa|kaun sa|top product|low stock)\b");
    }

    private static bool IsGenerationPrefix(string message, int index)
    {
        var prefix = message[..Math.Max(0, index)];
        return GenerationPrefixRegex().IsMatch(prefix);
    }

    private static string TrimName(string raw)
    {
        var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var kept = new List<string>();
        foreach (var token in tokens)
        {
            var bare = token.Trim('.', ',', ':', ';');
            if (bare.Length == 0)
                continue;
            if (bare.Equals("laptop", StringComparison.OrdinalIgnoreCase)
                || bare.Equals("laptops", StringComparison.OrdinalIgnoreCase)
                || bare.Equals("order", StringComparison.OrdinalIgnoreCase)
                || bare.Equals("product", StringComparison.OrdinalIgnoreCase)
                || bare.Equals("quantity", StringComparison.OrdinalIgnoreCase)
                || bare.Equals("qty", StringComparison.OrdinalIgnoreCase)
                || bare.Equals("hp", StringComparison.OrdinalIgnoreCase)
                || bare.Equals("generation", StringComparison.OrdinalIgnoreCase)
                || bare.Equals("gen", StringComparison.OrdinalIgnoreCase))
                break;
            if (!Regex.IsMatch(bare, @"^[A-Za-z][A-Za-z'.-]*$"))
                break;
            kept.Add(bare);
            if (kept.Count == 4)
                break;
        }

        var name = string.Join(' ', kept).Trim();
        if (name.Equals("this", StringComparison.OrdinalIgnoreCase)
            || name.Equals("the", StringComparison.OrdinalIgnoreCase)
            || name.Equals("that", StringComparison.OrdinalIgnoreCase))
            return "";
        return name;
    }

    private static bool TryDecimal(string raw, out decimal quantity) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity)
        || decimal.TryParse(raw, NumberStyles.Number, CultureInfo.CurrentCulture, out quantity);

    private static string? Get(Dictionary<string, string?> slots, string key) =>
        slots.TryGetValue(key, out var value) ? value : null;

    private static string Normalize(string message)
    {
        var text = message.Trim().ToLowerInvariant().Replace("'", "");
        text = Regex.Replace(text, @"[!.?,]", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    [GeneratedRegex(@"\b(?:qty|quantity)\s*(?:is|:|=)?\s*(\d{1,5}(?:\.\d+)?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LabeledQuantityRegex();

    [GeneratedRegex(@"\b(one|two|three|four|five|six|seven|eight|nine|ten)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NumberWordRegex();

    [GeneratedRegex(@"\b\d{1,3}(?:,\d{3})+(?:\.\d+)?\b|\b\d{1,5}(?:\.\d+)?\b")]
    private static partial Regex BareNumberRegex();

    [GeneratedRegex(@"(?<!generation\s)(?<!gen\s)(?<!model\s)(?<!version\s)\b\d{1,5}(?:\.\d+)?\s+(?=[A-Za-z])", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingCountRegex();

    [GeneratedRegex(@"(?:generation|gen|model|version|v|#)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex GenerationPrefixRegex();

    [GeneratedRegex(@"\bfor\s+(?:the\s+)?(?:customer|client\s+)?([A-Za-z][A-Za-z' .-]{1,80})", RegexOptions.IgnoreCase)]
    private static partial Regex CustomerNameRegex();

    [GeneratedRegex(@"\b(?:name\s+is|named|customer\s+name\s+is)\s+([A-Za-z][A-Za-z'.-]{1,40})", RegexOptions.IgnoreCase)]
    private static partial Regex NameIsRegex();

    private sealed record ChoiceRow(Guid Id, string Label);
}

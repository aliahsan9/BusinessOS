using System.Text;
using System.Text.RegularExpressions;
using BusinessOS.Application.Features.Agents.Enums;

namespace BusinessOS.Application.Features.Agents;

/// <summary>
/// Picks Sophia's reply language from the current message, an explicit switch, or the
/// conversation preference. Routing cues let existing English tool matchers understand
/// Urdu, Roman Urdu, and Arabic without a second intent model.
/// </summary>
public static partial class ConversationLanguage
{
    public readonly record struct Resolution(string Language, bool ExplicitSwitch);

    public static Resolution Resolve(string? message, string? stickyPreference, bool preferenceLocked = false)
    {
        var sticky = AgentLanguages.Coalesce(stickyPreference);
        var text = message?.Trim() ?? "";
        if (text.Length == 0)
            return new Resolution(sticky, false);

        if (TryExplicitSwitch(text, out var switched))
            return new Resolution(switched, true);

        if (preferenceLocked)
            return new Resolution(sticky, false);

        if (TryScript(text, out var scriptLanguage))
            return new Resolution(scriptLanguage, false);

        if (RomanScore(text) >= 2)
            return new Resolution(AgentLanguages.RomanUrdu, false);

        if (IsSubstantialEnglish(text))
            return new Resolution(AgentLanguages.English, false);

        return new Resolution(sticky, false);
    }

    public static string ResolveTurn(
        string? message,
        string? requestLanguage,
        string? memoryLanguage,
        string? voiceLanguage)
    {
        var locked = AgentLanguages.TryGetConcrete(requestLanguage, out var lockedCode);
        var sticky = locked
            ? lockedCode
            : AgentLanguages.Coalesce(memoryLanguage, voiceLanguage);
        return Resolve(message, sticky, locked).Language;
    }

    public static string ResponseInstruction(string? language)
    {
        var code = AgentLanguages.Normalize(language);
        var body = code switch
        {
            AgentLanguages.RomanUrdu =>
                "Reply in Roman Urdu using Latin letters, the way a Pakistani colleague writes. Keep English business words (sales, invoice, SKU, stock, customer) when they are the natural term. Do not translate customer names, product names, SKUs, or IDs.",
            AgentLanguages.Urdu =>
                "Reply in natural Urdu using Urdu script. Keep English business words (invoice, SKU, stock) when they are clearer than a forced translation. Do not translate customer names, product names, SKUs, or IDs.",
            AgentLanguages.Arabic =>
                "Reply in natural Modern Standard Arabic with correct grammar. Keep customer names, product names, SKUs, and IDs unchanged.",
            _ =>
                "Reply in natural, conversational English."
        };

        return body + " Do not switch language unless the user asks. Do not greet again if the conversation is already underway. Treat short follow-ups such as \"why?\" as questions about the previous answer.";
    }

    /// <summary>
    /// Appends English routing words so tool selection and date ranges work in Urdu and Arabic.
    /// The original message is preserved for the model and for entity extraction.
    /// </summary>
    public static string ExpandForRouting(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "";

        var text = message;
        var cues = new List<string>();

        if (MentionsLastMonth(text))
            cues.Add("last month");
        else if (MentionsThisMonth(text))
            cues.Add("this month");

        if (ContainsAny(text, "سیلز", "سيلز", "فروخت", "مبيعات", "مبیع", "سیل"))
            cues.Add("sales");
        if (ContainsAny(text, "آمدنی", "إيراد", "ایراد"))
            cues.Add("revenue");
        if (ContainsAny(text, "منافع", "ربح", "profit"))
            cues.Add("profit");
        if (ContainsAny(text, "کم اسٹاک", "لو اسٹاک", "low stock", "اسٹاک ختم"))
            cues.Add("low stock");
        if (ContainsAny(text, "ٹاپ کسٹمر", "top customer"))
            cues.Add("top customer");
        if (ContainsAny(text, "آج", "اليوم"))
            cues.Add("today");
        if (ContainsAny(text, "انوائس", "فاتورة", "فواتير"))
            cues.Add("invoice");
        if (ContainsAny(text, "متأخر", "اوورڈیو", "overdue", "unpaid"))
            cues.Add("overdue");
        if (ContainsAny(text, "گاہک", "عميل", "عملاء", "زبون", "کسٹمر", "كستمر"))
            cues.Add("customer");
        if (ContainsAny(text, "کتنی", "کتنا", "کتنے", "kitni", "kitna", "kitne", "كم"))
            cues.Add("how many total");
        if (ContainsAny(text, "کل", "المجموع", "بلغت"))
            cues.Add("total");
        if (IsBestSellerCue(text))
            cues.Add("best selling products");
        if (IsStockOnHandCue(text))
            cues.Add("inventory stock");
        if (IsCreateCustomerCue(text))
            cues.Add("create customer");

        return cues.Count == 0 ? text : text + " " + string.Join(' ', cues);
    }

    /// <summary>Ranking questions such as "kon sa product zyada sell ho raha hai".</summary>
    public static bool IsBestSellerCue(string? text) =>
        ContainsAny(text,
            "best selling", "best-selling", "top selling", "most sold",
            "zyada sell", "ziada sell", "zyada sale", "ziada sale",
            "zyada bech", "ziada bech", "sab se zyada", "sab se ziada",
            "kon sa product zyada", "kon sa product ziada",
            "kaun sa product zyada", "kaun sa product ziada",
            "الأكثر مبيعا", "الاكثر مبيعا", "الأكثر مبيع", "اكثر مبيع",
            "سب سے زیادہ فروخت", "زیادہ بکنے")
        || (ContainsAny(text, "kon sa", "kaun sa", "کون سا")
            && ContainsAny(text, "zyada", "ziada", "sell", "sale", "bech", "فروخت"));

    /// <summary>On-hand stock questions, not low-stock alerts.</summary>
    public static bool IsStockOnHandCue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (ContainsAny(text, "low stock", "out of stock", "dead stock", "km stock", "kam stock", "کم اسٹاک"))
            return false;

        return ContainsAny(text,
            "kitna stock", "kitni stock", "kitne stock", "stock baqi", "baqi stock",
            "stock left", "stock remaining", "remaining stock", "how much stock",
            "کتنا اسٹاک", "اسٹاک باقی", "کتنا سٹاک",
            "كم المخزون", "المخزون المتبقي", "المخزون");
    }

    public static bool IsCreateCustomerCue(string? text) =>
        ContainsAny(text,
            "create customer", "add customer", "new customer",
            "نیا کسٹمر", "نئے کسٹمر", "کسٹمر بنا", "گاہک بنا",
            "عميل جديد", "أضف عميلا", "اضف عميلا");

    public static bool MentionsLastMonth(string? text) =>
        ContainsAny(text,
            "last month", "previous month",
            "پچھلے مہین", "الشهر الماضي", "الشهر الماضى",
            "pichle mahine", "pichlay mahine", "pichle mahina", "pichlay mahina");

    public static bool MentionsThisMonth(string? text) =>
        !MentionsLastMonth(text)
        && ContainsAny(text,
            "this month",
            "اس مہین", "اس مهين", "هذا الشهر",
            "is mahine", "is mahina", "iss mahine", "iss mahina",
            "mahine", "mahina");

    private static bool TryExplicitSwitch(string text, out string language)
    {
        if (ContainsAny(text, "roman urdu", "urdu roman"))
        {
            language = AgentLanguages.RomanUrdu;
            return true;
        }

        if (ContainsAny(text, "in english", "to english", "speak english", "انگریزی میں", "بالإنجليزية"))
        {
            language = AgentLanguages.English;
            return true;
        }

        if (ContainsAny(text, "in arabic", "to arabic", "speak arabic", "بالعربية", "بالعربي", "بالعربى"))
        {
            language = AgentLanguages.Arabic;
            return true;
        }

        if (ContainsAny(text, "in urdu", "to urdu", "speak urdu", "urdu mein", "urdu main", "اردو میں", "اردو مين", "اب اردو"))
        {
            language = AgentLanguages.Urdu;
            return true;
        }

        language = AgentLanguages.English;
        return false;
    }

    private static bool TryScript(string text, out string language)
    {
        var urdu = 0;
        var arabic = 0;
        var script = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!IsArabicScript(rune))
                continue;
            script++;
            if (UrduMarks.Contains(rune.Value))
                urdu++;
            else if (ArabicMarks.Contains(rune.Value))
                arabic++;
        }

        if (script < 3)
        {
            language = AgentLanguages.English;
            return false;
        }

        language = urdu > arabic || (urdu > 0 && arabic == 0)
            ? AgentLanguages.Urdu
            : AgentLanguages.Arabic;
        return true;
    }

    private static bool IsSubstantialEnglish(string text)
    {
        if (RomanScore(text) >= 2)
            return false;

        var latin = 0;
        foreach (var ch in text)
        {
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                latin++;
        }

        if (latin < 4)
            return false;

        if (text.Length >= 24)
            return true;

        return text.Length >= 10 && EnglishCueRegex().IsMatch(text);
    }

    private static int RomanScore(string text)
    {
        var score = 0;
        foreach (var (word, weight) in RomanTokens)
        {
            if (ContainsWord(text, word))
                score += weight;
        }

        return score;
    }

    private static bool ContainsWord(string text, string word) =>
        Regex.IsMatch(
            text,
            $@"(?<!\p{{L}}){Regex.Escape(word)}(?!\p{{L}})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool ContainsAny(string? text, params string[] terms) =>
        !string.IsNullOrWhiteSpace(text)
        && terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool IsArabicScript(Rune rune) =>
        rune.Value is >= 0x0600 and <= 0x06FF
            or >= 0x0750 and <= 0x077F
            or >= 0x08A0 and <= 0x08FF
            or >= 0xFB50 and <= 0xFDFF
            or >= 0xFE70 and <= 0xFEFF;

    private static readonly HashSet<int> UrduMarks =
    [
        0x0679, 0x067E, 0x0686, 0x0688, 0x0691, 0x0698, 0x06A9, 0x06AF,
        0x06BA, 0x06BE, 0x06C1, 0x06CC, 0x06D2
    ];

    private static readonly HashSet<int> ArabicMarks =
    [
        0x0622, 0x0623, 0x0625, 0x0626, 0x0624, 0x0629, 0x062B, 0x0630,
        0x0638, 0x0643, 0x0649, 0x064A
    ];

    private static readonly (string Word, int Weight)[] RomanTokens =
    [
        ("mujhe", 2), ("mjhe", 2), ("kitni", 2), ("kitna", 2), ("kitne", 2),
        ("batao", 2), ("btao", 2), ("dikhao", 2), ("dikha", 2),
        ("mahine", 2), ("mahina", 2), ("maheene", 2),
        ("chahiye", 2), ("chahie", 2), ("kaun", 2), ("kaunse", 2), ("konse", 2),
        ("hain", 2), ("karo", 2), ("kro", 2), ("liye", 2), ("shukriya", 2),
        ("zyada", 2), ("ziada", 2), ("raha", 2), ("rha", 2), ("baqi", 2), ("baki", 2),
        ("kon", 2),
        ("ki", 1), ("ka", 1), ("ke", 1), ("ko", 1), ("se", 1),
        ("mein", 1), ("mera", 1), ("meri", 1), ("mere", 1), ("kya", 1), ("hai", 1)
    ];

    [GeneratedRegex(
        @"\b(show|what|why|how|please|total|sales|invoice|stock|order|month|revenue)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EnglishCueRegex();
}

using System.Globalization;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Features.Agents.Enums;
using BusinessOS.Application.Features.AI.DTOs;

namespace BusinessOS.Infrastructure.AI;

internal static class AiNaturalReplyBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string BuildConversationalReply(
        string message,
        AiPageContextDto page,
        string? agentDisplayName = null,
        string? language = null)
    {
        var lower = message.Trim().ToLowerInvariant();
        var name = string.IsNullOrWhiteSpace(agentDisplayName) ? "Sophia" : agentDisplayName.Trim();
        if (AgentLanguages.TryGetConcrete(language, out var replyLanguage))
        {
            var localized = LocalizedSmallTalk(message, lower, name, replyLanguage);
            if (localized is not null)
                return localized;
        }

        if (IsChatbotDifference(lower))
            return $"The difference is that I am {name}, tied to this BusinessOS workspace. I can look up your stock, sales, customers, and invoices, and I only create a record after you confirm it. A normal chatbot answers from general text and cannot see your books.";

        if (lower.Contains("business intelligence"))
            return "Business intelligence means reading what already happened in the business — sales, stock, invoices, and customers — and saying what deserves attention. I use your workspace numbers when you ask for them, and I say so when there is nothing to report.";

        if (lower.Contains("accent") || lower.Contains("change your voice") || lower.Contains("your voice"))
            return "I speak with the voice selected in this chat. Change it from the voice list, or under Settings → Sophia Voice.";

        if (lower.Contains("thank") || lower.Contains("shukriya") || lower.Contains("shukria"))
        {
            return "You're welcome. Tell me if you need anything on inventory, orders, customers, or reports.";
        }

        if (lower.Contains("bye") || lower.Contains("goodbye"))
        {
            return $"Take care — I'm {name}, here whenever you need me.";
        }

        if (IsBareCancel(lower))
            return "There is nothing waiting to cancel. Tell me what you want to do next.";

        if (IsOpenQuestion(lower))
            return Clarify(language);

        if (IsAdviceQuestion(lower))
            return BuildAdviceReply(message);

        // Short natural greeting — never dump revenue or a long capability brochure.
        if (IsGreeting(lower, message))
        {
            return $"Hi — I'm {name}. What should we tackle: an order, inventory, or a quick report?";
        }

        var contextHint = page.Module switch
        {
            "customers" when page.CustomerId is not null =>
                "You're on a customer — say \"Create order for this customer\" or \"Summarize this customer\".",
            "orders" when page.OrderId is not null =>
                "You're viewing an order — ask about progress, line items, or invoices.",
            "invoices" when page.InvoiceId is not null =>
                "You're on an invoice — ask about payment status or outstanding balance.",
            "customers" =>
                "On Customers — name a customer or open their detail page.",
            "invoices" =>
                "On Invoices — try \"Show overdue invoices\".",
            "orders" =>
                "On Orders — ask me to create a sale or check progress.",
            "analytics" =>
                "On Analytics — ask about revenue trends or top customers.",
            "purchase" or "purchases" or "suppliers" =>
                "On Purchases — try creating a PO or a supplier.",
            _ =>
                $"I'm {name}. I can create orders, suppliers, products, and customers — or check stock and sales. What do you need?"
        };

        return contextHint;
    }

    private static string? LocalizedSmallTalk(string original, string lower, string name, string language)
    {
        var thanks = lower.Contains("thank") || lower.Contains("shukriya") || lower.Contains("shukria")
            || original.Contains("شکریہ", StringComparison.Ordinal);
        var goodbye = lower.Contains("bye") || lower.Contains("goodbye") || original.Contains("خدا حافظ", StringComparison.Ordinal);
        var greeting = IsGreeting(lower, original);
        if (!thanks && !goodbye && !greeting)
        {
            if (language == AgentLanguages.Arabic && (original.Contains("العرب", StringComparison.Ordinal) || lower.Contains("arabic")))
                return $"حسنًا، سأتحدث معك بالعربية. أنا {name}، ويمكنني مراجعة المبيعات والمخزون والعملاء.";
            if (language == AgentLanguages.Urdu && (original.Contains("اردو", StringComparison.Ordinal) || lower.Contains("urdu")))
                return $"ٹھیک ہے، اب میں اردو میں جواب دوں گی۔ میں {name} ہوں۔";
            return null;
        }

        if (language == AgentLanguages.RomanUrdu)
        {
            if (thanks) return "Koi baat nahi. Sales, stock, ya invoices — jo chahiye batao.";
            if (goodbye) return $"Allah hafiz. Main {name} hoon, jab chaho wapas aa jana.";
            return $"Hi, main {name} hoon. Order, stock, ya is month ki sales — kya dekhna hai?";
        }

        if (language == AgentLanguages.Urdu)
        {
            if (thanks) return "کوئی بات نہیں۔ سیلز، اسٹاک، یا انوائس میں سے جو دیکھنا ہو بتائیں۔";
            if (goodbye) return $"اللہ حافظ۔ میں {name} ہوں، جب چاہیں واپس آ جائیں۔";
            return $"سلام، میں {name} ہوں۔ آرڈر، اسٹاک، یا اس مہینے کی سیلز — کیا دیکھنا ہے؟";
        }

        if (language == AgentLanguages.Arabic)
        {
            if (thanks) return "على الرحب. قل لي إن احتجت المبيعات أو المخزون أو الفواتير.";
            if (goodbye) return $"إلى اللقاء. أنا {name}، أعود متى احتجت.";
            return $"مرحباً، أنا {name}. هل نراجع طلبًا أو المخزون أو مبيعات هذا الشهر؟";
        }

        return null;
    }

    private static bool IsGreeting(string lower, string original) =>
        lower is "hi" or "hello" or "hey" or "salam" or "assalam" or "assalamualaikum"
        || lower.StartsWith("hi ") || lower.StartsWith("hello") || lower.StartsWith("hey ")
        || lower.StartsWith("good morning") || lower.StartsWith("good afternoon") || lower.StartsWith("good evening")
        || lower.StartsWith("salam") || lower.Contains("assalam")
        || original.Contains("سلام", StringComparison.OrdinalIgnoreCase)
        || original.Contains("السلام", StringComparison.OrdinalIgnoreCase);

    private static bool IsBareCancel(string lower)
    {
        var text = lower.Trim().TrimEnd('!', '.', '?');
        return text is "cancel" or "stop" or "never mind" or "nevermind" or "nahi" or "nai" or "mat karo" or "rehne do";
    }

    private static bool IsChatbotDifference(string lower) =>
        lower.Contains("difference") && (lower.Contains("chatbot") || lower.Contains("chat bot"));

    private static bool IsOpenQuestion(string lower)
    {
        if (IsAdviceQuestion(lower))
            return false;
        if (lower.Contains("what can you") || lower.Contains("who are you") || lower.Contains("what do you"))
            return false;

        return lower.Contains('?')
            || lower.StartsWith("what ")
            || lower.StartsWith("who ")
            || lower.StartsWith("why ")
            || lower.StartsWith("which ")
            || lower.StartsWith("how ");
    }

    private static string Clarify(string? language) => AgentLanguages.Normalize(language) switch
    {
        AgentLanguages.Urdu =>
            "میں سیلز، اسٹاک، کسٹمر، آرڈر یا انوائس دیکھ سکتا ہوں، اور تصدیق کے بعد بنا بھی سکتا ہوں۔ ان میں سے کون سا کام مراد ہے؟",
        AgentLanguages.RomanUrdu =>
            "Main sales, stock, customer, order, ya invoice dekh sakta hoon, aur confirm karne ke baad bana bhi sakta hoon. In mein se kaun sa kaam murad hai?",
        AgentLanguages.Arabic =>
            "يمكنني مراجعة المبيعات أو المخزون أو العملاء أو الطلبات أو الفواتير، أو إنشاء أحدها بعد التأكيد. أيها تقصد؟",
        _ =>
            "I can look up sales, stock, customers, orders, or invoices, or create one of those after you confirm the details. Which of those did you mean?"
    };

    public static string BuildAdviceReply(string message)
    {
        var lower = message.ToLowerInvariant();
        var topic = ContainsAny(lower, "laptop", "computer", "notebook") ? "laptop"
            : ContainsAny(lower, "sales", "sell", "revenue") ? "sales"
            : ContainsAny(lower, "customer") ? "customer"
            : "business";

        return topic switch
        {
            "laptop" =>
                """
                Great question — here are practical ways to increase daily laptop sales:

                1. Push today’s bestsellers — Feature 1–2 models with clear price/value (student, gaming, office) on your landing page and socials.
                2. Bundle to raise ticket size — Offer laptop + bag/mouse/warranty packages so average order value climbs without discounting the device.
                3. Follow up warm leads same day — Call or message quotes that didn’t convert; a short “still available / limited stock” nudge often closes sales.
                4. Make financing obvious — Highlight installment / EMI options next to every price; many buyers stall on upfront cost.
                5. Track what actually sells — Ask me “What are my sales this month?” or “Top products sold” so you double down on winners and cut slow movers.

                Want me to pull your current sales numbers so we can tailor this to what’s working for you?
                """,
            "sales" =>
                """
                Here’s a focused playbook to grow daily sales:

                1. Protect the pipeline — Follow up open quotes and unpaid invoices before chasing cold traffic.
                2. Win more from existing customers — Upsell related products/services to people who already trust you.
                3. Tighten the offer — Clear pricing, bundles, and a simple reason to buy today (stock, promo, or bonus).
                4. Measure daily — Know units sold, revenue, and top customers so you can repeat what works.
                5. Remove friction — Faster quotes, easier payment, and quicker delivery beat most “marketing tricks.”

                If you want data-backed next steps, ask “What is our revenue this month?” or “Who are the top customers by revenue?”
                """,
            "customer" =>
                """
                To strengthen customer growth and retention:

                1. Segment active vs inactive buyers and re-engage the quiet ones with a personal offer.
                2. Shorten response time on quotes and support — speed often beats price.
                3. Ask for referrals from your happiest customers after a successful delivery.
                4. Keep a simple CRM habit: next action + due date on every open opportunity.

                I can also summarize a specific customer or list overdue invoices if that helps.
                """,
            _ =>
                """
                Happy to help. A few solid moves for most businesses:

                1. Focus on one clear offer and make buying easy.
                2. Follow up leads and invoices the same day.
                3. Improve repeat purchase from existing customers.
                4. Use your live numbers to decide what to push next.

                Ask me things like “Show overdue invoices”, “Revenue this month”, or tell me the product/channel you want to grow and I’ll go deeper.
                """
        };
    }

    private static bool IsAdviceQuestion(string lower) =>
        ContainsAny(lower,
            "how can i", "how should i", "increase", "improve", "boost", "grow my", "grow our",
            "tips", "advice", "strateg", "recommend", "ways to", "best way to");

    public static string BuildHelpReply(string message)
    {
        var lower = message.ToLowerInvariant();

        if (lower.Contains("urdu"))
            return "ٹھیک ہے، اب میں اردو رسم الخط میں جواب دوں گی۔ رومن اردو بھی ٹھیک ہے۔";

        if (lower.Contains("arabic") || lower.Contains("عربي"))
            return "حسناً، سأجيب بالعربية.";

        if (lower.Contains("english"))
            return "I'll reply in English.";

        if (lower.Contains("who are you") || lower.Contains("what can you") || lower.Contains("what you can") || lower.Contains("what do you do") || lower.Contains("your name"))
            return "I'm Sophia, the business analyst in BusinessOS. I can look up sales, stock, invoices, and customers, and I can create customers, products, and orders after you confirm the details. Ask in English, Urdu, or Arabic. Say \"cancel\" if you want me to drop something I started.";

        if (ContainsWord(lower, "accent") || ContainsWord(lower, "voice"))
            return "I speak with the voice selected in this chat. Change it from the voice list here, or under Settings → Sophia Voice. Spoken replies are AI-generated.";

        if (lower.Contains("customer"))
            return "To create a customer, go to Customers → New Customer. Enter name, email, phone, and address. Customers link to orders, invoices, and analytics.";

        if (lower.Contains("project") || lower.Contains("order"))
            return "Projects are managed as Orders. Go to Orders → New Order, pick a customer, add line items, and track status through completion.";

        if (lower.Contains("invoice"))
            return "Create invoices from completed orders on the Invoices page or from the order detail screen. Track payments and outstanding balances there.";

        if (lower.Contains("task"))
            return "Tasks appear as order line items. Add products/services when creating or editing an order to represent work items.";

        return "Welcome to BusinessOS! I can answer questions using your real business data — customers, orders, invoices, and analytics. Try asking \"Show overdue invoices\" or open a customer and say \"Summarize this customer\".";
    }

    public static string BuildBusinessReply(string message, AiContextDto context)
    {
        var lower = message.ToLowerInvariant();

        if (context.Customer is not null && ContainsAny(lower, "summarize", "summary", "about", "this customer", "tell me", "who is", "describe"))
            return BuildCustomerSummary(context);

        if (context.Customer is not null && ContainsAny(lower, "revenue", "spending", "spent", "analytics"))
            return BuildCustomerAnalytics(context);

        if (ContainsAny(lower, "overdue", "unpaid", "outstanding", "past due"))
            return BuildOverdueInvoicesSummary(context);

        if (ContainsAny(lower, "revenue", "highest", "top customer", "most revenue", "best customer"))
            return BuildRevenueRankingSummary(context);

        if (ContainsAny(lower, "project progress", "delayed", "task", "workload", "progress"))
            return BuildProjectProgressSummary(context);

        if (context.Invoices.Count > 0 && context.Orders.Count == 0 && context.Customer is null)
            return BuildOverdueInvoicesSummary(context);

        if (context.Customer is not null)
            return BuildCustomerSummary(context);

        if (context.Orders.Count > 0)
            return BuildOrdersSummary(context);

        if (context.Invoices.Count > 0)
            return BuildInvoicesListSummary(context);

        return "I couldn't find relevant business data for that question. Open a customer, order, or invoice detail page, or try \"Show overdue invoices\" or \"Which customers generated highest revenue?\"";
    }

    private static string BuildCustomerSummary(AiContextDto context)
    {
        var c = ToElement(context.Customer);
        if (c is null)
            return "No customer data is available in the current context.";

        var name = GetString(c, "fullName", "FullName") ?? "This customer";
        var email = GetString(c, "email", "Email") ?? "—";
        var phone = GetString(c, "phoneNumber", "PhoneNumber") ?? "—";
        var city = GetString(c, "city", "City");
        var country = GetString(c, "country", "Country");
        var location = string.Join(", ", new[] { city, country }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var active = GetBool(c, "isActive", "IsActive");

        var sb = new StringBuilder();
        sb.AppendLine($"{name}");
        sb.AppendLine($"• Email: {email}");
        sb.AppendLine($"• Phone: {phone}");
        if (!string.IsNullOrWhiteSpace(location))
            sb.AppendLine($"• Location: {location}");
        sb.AppendLine($"• Status: {(active ? "Active" : "Inactive")}");

        if (context.Analytics is not null)
        {
            var a = ToElement(context.Analytics);
            if (a is not null)
            {
                sb.AppendLine();
                sb.AppendLine("Activity");
                sb.AppendLine($"• Total orders: {GetInt(a, "totalOrders", "TotalOrders")}");
                sb.AppendLine($"• Total spending: {FormatMoney(GetDecimal(a, "totalSpending", "TotalSpending"))}");
                sb.AppendLine($"• Average order value: {FormatMoney(GetDecimal(a, "averageOrderValue", "AverageOrderValue"))}");
                var lastOrder = GetString(a, "lastOrderDate", "LastOrderDate");
                if (!string.IsNullOrWhiteSpace(lastOrder) && DateTime.TryParse(lastOrder, out var dt))
                    sb.AppendLine($"• Last order: {dt:MMM d, yyyy}");
            }
        }

        if (context.Orders.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Recent orders ({context.Orders.Count})");
            foreach (var order in context.Orders.Take(5))
            {
                var o = ToElement(order);
                if (o is null) continue;
                sb.AppendLine($"• {GetString(o, "orderNumber", "OrderNumber")} — {GetString(o, "status", "Status")}, {FormatMoney(GetDecimal(o, "grandTotal", "GrandTotal"))}");
            }
        }

        if (context.Invoices.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Invoices ({context.Invoices.Count})");
            foreach (var inv in context.Invoices.Take(5))
            {
                var i = ToElement(inv);
                if (i is null) continue;
                var outstanding = GetDecimal(i, "outstandingAmount", "OutstandingAmount");
                sb.AppendLine($"• {GetString(i, "invoiceNumber", "InvoiceNumber")} — {GetString(i, "status", "Status")}, outstanding {FormatMoney(outstanding)}");
            }
        }

        return sb.ToString().Trim();
    }

    private static string BuildCustomerAnalytics(AiContextDto context)
    {
        if (context.Analytics is null)
            return "No analytics data is available for this customer.";

        var a = ToElement(context.Analytics);
        if (a is null)
            return "No analytics data is available for this customer.";

        var name = GetString(ToElement(context.Customer), "fullName", "FullName") ?? "This customer";
        return $"{name} has {GetInt(a, "totalOrders", "TotalOrders")} order(s) with total spending of {FormatMoney(GetDecimal(a, "totalSpending", "TotalSpending"))} " +
               $"and an average order value of {FormatMoney(GetDecimal(a, "averageOrderValue", "AverageOrderValue"))}. " +
               $"{GetInt(a, "totalCompletedOrders", "TotalCompletedOrders")} order(s) are completed.";
    }

    private static string BuildOverdueInvoicesSummary(AiContextDto context)
    {
        if (context.Invoices.Count == 0)
            return "Good news — there are no overdue or unpaid invoices in your records right now.";

        var sb = new StringBuilder();
        var analytics = ToElement(context.Analytics);
        if (analytics is not null)
        {
            sb.AppendLine($"Found {GetInt(analytics, "totalOverdue", "TotalOverdue")} overdue invoice(s) totaling {FormatMoney(GetDecimal(analytics, "totalOutstanding", "TotalOutstanding"))} outstanding.");
        }
        else
        {
            sb.AppendLine($"Found {context.Invoices.Count} overdue or unpaid invoice(s):");
        }

        sb.AppendLine();
        foreach (var inv in context.Invoices.Take(10))
        {
            var i = ToElement(inv);
            if (i is null) continue;
            var customer = GetString(i, "customerName", "CustomerName") ?? "Unknown";
            var days = GetInt(i, "daysOverdue", "DaysOverdue");
            sb.AppendLine($"• {GetString(i, "invoiceNumber", "InvoiceNumber")} — {customer}, outstanding {FormatMoney(GetDecimal(i, "outstandingAmount", "OutstandingAmount"))}" +
                          (days > 0 ? $" ({days} days overdue)" : ""));
        }

        if (context.Invoices.Count > 10)
            sb.AppendLine($"\n…and {context.Invoices.Count - 10} more.");

        return sb.ToString().Trim();
    }

    private static string BuildRevenueRankingSummary(AiContextDto context)
    {
        var analytics = ToElement(context.Analytics);
        if (analytics is null)
            return "No revenue ranking data is available yet.";

        JsonElement top;
        if (!analytics.Value.TryGetProperty("topCustomersByRevenue", out top)
            && !analytics.Value.TryGetProperty("TopCustomersByRevenue", out top))
        {
            return "No revenue ranking data is available yet.";
        }

        var sb = new StringBuilder("Top customers by revenue:\n");
        var rank = 1;
        foreach (var item in top.EnumerateArray().Take(10))
        {
            sb.AppendLine($"{rank}. {GetString(item, "customerName", "CustomerName")} — {FormatMoney(GetDecimal(item, "totalRevenue", "TotalRevenue"))} ({GetInt(item, "orderCount", "OrderCount")} orders)");
            rank++;
        }

        return sb.ToString().Trim();
    }

    private static string BuildProjectProgressSummary(AiContextDto context)
    {
        var sb = new StringBuilder();
        var analytics = ToElement(context.Analytics);
        if (analytics is not null)
        {
            sb.AppendLine($"Active projects: {GetInt(analytics, "activeProjects", "ActiveProjects")} | Delayed tasks: {GetInt(analytics, "delayedTasks", "DelayedTasks")}");
            sb.AppendLine();
        }

        if (context.Orders.Count > 0)
        {
            sb.AppendLine("Recent projects (orders):");
            foreach (var order in context.Orders.Take(8))
            {
                var o = ToElement(order);
                if (o is null) continue;
                sb.AppendLine($"• {GetString(o, "orderNumber", "OrderNumber")} — {GetString(o, "customerName", "CustomerName")}, {GetString(o, "status", "Status")}, {GetInt(o, "taskCount", "TaskCount")} item(s)");
            }
        }

        if (context.Projects.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Delayed tasks:");
            foreach (var task in context.Projects.Take(8))
            {
                var t = ToElement(task);
                if (t is null) continue;
                var due = GetString(t, "dueDate", "DueDate");
                var dueLabel = !string.IsNullOrWhiteSpace(due) && due.Length >= 10 ? due[..10] : "—";
                sb.AppendLine($"• {GetString(t, "title", "Title")} ({GetString(t, "projectName", "ProjectName")}) — due {dueLabel}");
            }
        }

        return sb.Length > 0
            ? sb.ToString().Trim()
            : "No project or task data is available.";
    }

    private static string BuildOrdersSummary(AiContextDto context)
    {
        var sb = new StringBuilder("Orders / projects:\n");
        foreach (var order in context.Orders.Take(10))
        {
            var o = ToElement(order);
            if (o is null) continue;
            sb.AppendLine($"• {GetString(o, "orderNumber", "OrderNumber")} — {GetString(o, "status", "Status")}, {FormatMoney(GetDecimal(o, "grandTotal", "GrandTotal"))}");
        }
        return sb.ToString().Trim();
    }

    private static string BuildInvoicesListSummary(AiContextDto context)
    {
        var sb = new StringBuilder("Invoices:\n");
        foreach (var inv in context.Invoices.Take(10))
        {
            var i = ToElement(inv);
            if (i is null) continue;
            sb.AppendLine($"• {GetString(i, "invoiceNumber", "InvoiceNumber")} — {GetString(i, "status", "Status")}, {FormatMoney(GetDecimal(i, "grandTotal", "GrandTotal"))}");
        }
        return sb.ToString().Trim();
    }

    private static JsonElement? ToElement(object? value)
    {
        if (value is null) return null;
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
    }

    private static string? GetString(JsonElement? el, params string[] names)
    {
        if (el is null) return null;
        foreach (var name in names)
        {
            if (el.Value.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        return null;
    }

    private static int GetInt(JsonElement? el, params string[] names)
    {
        if (el is null) return 0;
        foreach (var name in names)
        {
            if (el.Value.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var v))
                return v;
        }
        return 0;
    }

    private static decimal GetDecimal(JsonElement? el, params string[] names)
    {
        if (el is null) return 0;
        foreach (var name in names)
        {
            if (!el.Value.TryGetProperty(name, out var prop)) continue;
            if (prop.TryGetDecimal(out var d)) return d;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var dbl))
                return (decimal)dbl;
        }
        return 0;
    }

    private static bool GetBool(JsonElement? el, params string[] names)
    {
        if (el is null) return true;
        foreach (var name in names)
        {
            if (el.Value.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return prop.GetBoolean();
        }
        return true;
    }

    private static string FormatMoney(decimal amount) =>
        amount.ToString("C2", CultureInfo.InvariantCulture);

    private static bool ContainsWord(string text, string word)
    {
        var index = 0;
        while ((index = text.IndexOf(word, index, StringComparison.Ordinal)) >= 0)
        {
            var before = index == 0 || !char.IsLetter(text[index - 1]);
            var afterIndex = index + word.Length;
            var after = afterIndex >= text.Length || !char.IsLetter(text[afterIndex]);
            if (before && after)
                return true;
            index += word.Length;
        }

        return false;
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}


using System.Globalization;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Features.AI.DTOs;

namespace BusinessOS.Infrastructure.AI;

internal static class AiNaturalReplyBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string BuildConversationalReply(string message, AiPageContextDto page)
    {
        var lower = message.Trim().ToLowerInvariant();

        if (lower.Contains("thank"))
        {
            return "You're welcome! Let me know if you need anything else about your customers, projects, or invoices.";
        }

        if (lower.Contains("bye") || lower.Contains("goodbye"))
        {
            return "Goodbye! I'll be here whenever you need help with BusinessOS.";
        }

        var contextHint = page.Module switch
        {
            "customers" when page.CustomerId is not null =>
                "You're on a customer detail page — ask me to summarize this customer, show their revenue, or list unpaid invoices.",
            "orders" when page.OrderId is not null =>
                "You're viewing a project (order) — ask about progress, line items, or related invoices.",
            "invoices" when page.InvoiceId is not null =>
                "You're on an invoice — ask about payment status, outstanding balance, or overdue invoices.",
            "customers" => "You're on the Customers page — ask about a specific customer or say \"summarize this customer\" on a detail page.",
            "invoices" => "You're on Invoices — try \"Show overdue invoices\" or \"Revenue this month\".",
            "orders" => "You're on Projects — try \"Project progress\" or \"Delayed tasks\".",
            "analytics" => "You're on Analytics — ask about revenue trends or top customers.",
            _ => "Ask me about customers, projects, invoices, revenue, or overdue payments — I pull answers from your live business data."
        };

        return $"Hello! I'm BusinessOS AI, your business assistant. {contextHint}";
    }

    public static string BuildHelpReply(string message)
    {
        var lower = message.ToLowerInvariant();

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
        sb.AppendLine($"**{name}**");
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
                sb.AppendLine("**Activity**");
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
            sb.AppendLine($"**Recent orders ({context.Orders.Count})**");
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
            sb.AppendLine($"**Invoices ({context.Invoices.Count})**");
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
            sb.AppendLine($"Found **{GetInt(analytics, "totalOverdue", "TotalOverdue")}** overdue invoice(s) totaling **{FormatMoney(GetDecimal(analytics, "totalOutstanding", "TotalOutstanding"))}** outstanding.");
        }
        else
        {
            sb.AppendLine($"Found **{context.Invoices.Count}** overdue or unpaid invoice(s):");
        }

        sb.AppendLine();
        foreach (var inv in context.Invoices.Take(10))
        {
            var i = ToElement(inv);
            if (i is null) continue;
            var customer = GetString(i, "customerName", "CustomerName") ?? "Unknown";
            var days = GetInt(i, "daysOverdue", "DaysOverdue");
            sb.AppendLine($"• **{GetString(i, "invoiceNumber", "InvoiceNumber")}** — {customer}, outstanding {FormatMoney(GetDecimal(i, "outstandingAmount", "OutstandingAmount"))}" +
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

        var sb = new StringBuilder("**Top customers by revenue:**\n");
        var rank = 1;
        foreach (var item in top.EnumerateArray().Take(10))
        {
            sb.AppendLine($"{rank}. **{GetString(item, "customerName", "CustomerName")}** — {FormatMoney(GetDecimal(item, "totalRevenue", "TotalRevenue"))} ({GetInt(item, "orderCount", "OrderCount")} orders)");
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
            sb.AppendLine($"Active projects: **{GetInt(analytics, "activeProjects", "ActiveProjects")}** | Delayed tasks: **{GetInt(analytics, "delayedTasks", "DelayedTasks")}**");
            sb.AppendLine();
        }

        if (context.Orders.Count > 0)
        {
            sb.AppendLine("**Recent projects (orders):**");
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
            sb.AppendLine("**Delayed tasks:**");
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
        var sb = new StringBuilder("**Orders / projects:**\n");
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
        var sb = new StringBuilder("**Invoices:**\n");
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

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}

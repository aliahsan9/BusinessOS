using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;

namespace BusinessOS.Infrastructure.Integrations;

/// <summary>
/// Shared request builders for catalog providers. Prefer <see cref="IIntegrationProvider"/> for execution.
/// </summary>
public static class IntegrationApi
{
    public static async Task<IntegrationCallResult> ExecuteAsync(
        HttpClient http,
        IntegrationProviderDefinition provider,
        string operation,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken)
    {
        var args = JsonNode.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson) as JsonObject ?? new JsonObject();
        var metadata = JsonNode.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson) as JsonObject ?? new JsonObject();
        var (method, url, body) = Build(provider.Key, operation, args, metadata);
        if (url is null)
        {
            return new IntegrationCallResult
            {
                Performed = false,
                Status = "not_enabled",
                Message = "That operation is not enabled for this provider."
            };
        }

        return await IntegrationHttpExecutor.SendAsync(
            http, provider.Key, operation, method, url, accessToken, body, cancellationToken);
    }

    public static (HttpMethod Method, string? Url, JsonObject? Body) Build(string provider, string operation, JsonObject args, JsonObject metadata)
    {
        var shop = Text(metadata, "shop");
        var site = Text(metadata, "site");
        var property = Text(metadata, "propertyId");
        var customer = Text(metadata, "customerId");
        var phoneNumberId = Text(metadata, "phoneNumberId");
        var igAccount = Text(metadata, "instagramBusinessAccountId");
        return (provider, operation) switch
        {
            ("gmail", "list") => (HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults=" + (Text(args, "maxResults") ?? "10") + "&q=" + Uri.EscapeDataString(Text(args, "query") ?? ""), null),
            ("gmail", "get") => (HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/messages/" + Required(args, "id") + "?format=full", null),
            ("gmail", "history") => (HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/history?startHistoryId=" + Uri.EscapeDataString(Text(args, "startHistoryId") ?? Text(metadata, "historyId") ?? "1"), null),
            ("gmail", "draft") => (HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/drafts", new JsonObject { ["message"] = new JsonObject { ["raw"] = ResolveGmailRaw(args) } }),
            ("gmail", "send") => (HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send", new JsonObject { ["raw"] = ResolveGmailRaw(args) }),
            ("google-calendar", "list") => (HttpMethod.Get, "https://www.googleapis.com/calendar/v3/calendars/primary/events?maxResults=10", null),
            ("google-calendar", "freebusy") => (HttpMethod.Post, "https://www.googleapis.com/calendar/v3/freeBusy", args),
            ("google-calendar", "create") => (HttpMethod.Post, "https://www.googleapis.com/calendar/v3/calendars/primary/events", args),
            ("google-calendar", "update") => (HttpMethod.Put, "https://www.googleapis.com/calendar/v3/calendars/primary/events/" + Required(args, "eventId"), args),
            ("google-calendar", "delete") => (HttpMethod.Delete, "https://www.googleapis.com/calendar/v3/calendars/primary/events/" + Required(args, "eventId"), null),
            ("google-drive", "list") => (HttpMethod.Get, "https://www.googleapis.com/drive/v3/files?pageSize=10&fields=files(id,name,mimeType)", null),
            ("google-drive", "get") => (HttpMethod.Get, "https://www.googleapis.com/drive/v3/files/" + Required(args, "fileId") + "?fields=id,name,mimeType", null),
            ("google-drive", "upload") => (HttpMethod.Post, "https://www.googleapis.com/upload/drive/v3/files?uploadType=media", args),
            ("github", "repos") => (HttpMethod.Get, "https://api.github.com/user/repos?per_page=10", null),
            ("github", "issues") => (HttpMethod.Get, "https://api.github.com/repos/" + Required(args, "owner") + "/" + Required(args, "repo") + "/issues?per_page=10", null),
            ("github", "pulls") => (HttpMethod.Get, "https://api.github.com/repos/" + Required(args, "owner") + "/" + Required(args, "repo") + "/pulls?per_page=10", null),
            ("github", "commits") => (HttpMethod.Get, "https://api.github.com/repos/" + Required(args, "owner") + "/" + Required(args, "repo") + "/commits?per_page=10", null),
            ("github", "createIssue") => (HttpMethod.Post, "https://api.github.com/repos/" + Required(args, "owner") + "/" + Required(args, "repo") + "/issues", new JsonObject { ["title"] = Text(args, "title") ?? "Issue", ["body"] = Text(args, "body") ?? "" }),
            ("slack", "channels") => (HttpMethod.Get, "https://slack.com/api/conversations.list?limit=20&types=public_channel", null),
            ("slack", "post") => (HttpMethod.Post, "https://slack.com/api/chat.postMessage", new JsonObject { ["channel"] = Text(args, "channel") ?? "", ["text"] = Text(args, "text") ?? "" }),
            ("microsoft-teams", "teams") => (HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/joinedTeams", null),
            ("microsoft-teams", "post") => (HttpMethod.Post, "https://graph.microsoft.com/v1.0/teams/" + Required(args, "teamId") + "/channels/" + Required(args, "channelId") + "/messages", new JsonObject { ["body"] = new JsonObject { ["content"] = Text(args, "text") ?? "" } }),
            ("whatsapp", "status") => (HttpMethod.Get, "https://graph.facebook.com/v21.0/" + Required(metadata, "phoneNumberId"), null),
            ("whatsapp", "sendTemplate") => WhatsAppSendTemplate(args, metadata, phoneNumberId),
            ("whatsapp", "sendText") => WhatsAppSendText(args, metadata, phoneNumberId),
            ("stripe", "balance") => (HttpMethod.Get, "https://api.stripe.com/v1/balance", null),
            ("stripe", "payment") => (HttpMethod.Get, "https://api.stripe.com/v1/payment_intents/" + Required(args, "paymentIntentId"), null),
            ("stripe", "payments") => (HttpMethod.Get, "https://api.stripe.com/v1/payment_intents?limit=" + (Text(args, "limit") ?? "10"), null),
            ("stripe", "refund") => (HttpMethod.Post, "https://api.stripe.com/v1/refunds", new JsonObject { ["payment_intent"] = Text(args, "paymentIntentId") ?? "" }),
            ("paypal", "order") => (HttpMethod.Get, "https://api-m.paypal.com/v2/checkout/orders/" + Required(args, "orderId"), null),
            ("paypal", "refund") => (HttpMethod.Post, "https://api-m.paypal.com/v2/payments/captures/" + Required(args, "captureId") + "/refund", new JsonObject()),
            ("linkedin", "organization") => (HttpMethod.Get, "https://api.linkedin.com/v2/organizationalEntityAcls?q=roleAssignee", null),
            ("meta", "pages") => (HttpMethod.Get, "https://graph.facebook.com/v21.0/me/accounts", null),
            ("meta", "insights") => MetaInsights(args, metadata),
            ("meta", "publish") => MetaPublish(args, metadata),
            ("meta", "instagramAccount") => (HttpMethod.Get, "https://graph.facebook.com/v21.0/" + Required(metadata, "pageId") + "?fields=instagram_business_account", null),
            ("meta", "publishInstagram") => InstagramPublish(args, metadata, igAccount),
            ("google-analytics", "report") => (HttpMethod.Post, "https://analyticsdata.googleapis.com/v1beta/properties/" + (property ?? Required(args, "propertyId")) + ":runReport", args),
            ("google-ads", "report") => (HttpMethod.Post, "https://googleads.googleapis.com/v18/customers/" + (customer ?? Required(args, "customerId")) + "/googleAds:search", args),
            ("shopify", "products") => (HttpMethod.Get, $"https://{shop}.myshopify.com/admin/api/2024-10/products.json?limit=10", null),
            ("shopify", "orders") => (HttpMethod.Get, $"https://{shop}.myshopify.com/admin/api/2024-10/orders.json?limit=10&status=any", null),
            ("shopify", "updateProduct") => (HttpMethod.Put, $"https://{shop}.myshopify.com/admin/api/2024-10/products/{Required(args, "productId")}.json", args),
            ("wordpress", "posts") => (HttpMethod.Get, "https://public-api.wordpress.com/rest/v1.1/sites/" + Uri.EscapeDataString(site ?? Required(args, "site")) + "/posts/?number=10", null),
            ("wordpress", "draft") => (HttpMethod.Post, "https://public-api.wordpress.com/rest/v1.1/sites/" + Uri.EscapeDataString(site ?? Required(args, "site")) + "/posts/new", args),
            ("google-business-profile", "accounts") => (HttpMethod.Get, "https://mybusinessaccountmanagement.googleapis.com/v1/accounts", null),
            ("google-business-profile", "locations") => (HttpMethod.Get, "https://mybusinessbusinessinformation.googleapis.com/v1/" + Required(metadata, "accountId") + "/locations?readMask=name,title,storefrontAddress,phoneNumbers", null),
            ("google-business-profile", "location") => (HttpMethod.Get, "https://mybusinessbusinessinformation.googleapis.com/v1/" + Required(metadata, "locationId") + "?readMask=name,title,storefrontAddress,phoneNumbers,websiteUri", null),
            ("google-business-profile", "performance") => (HttpMethod.Get, "https://businessprofileperformance.googleapis.com/v1/" + Required(metadata, "locationId") + ":getDailyMetricsTimeSeries?dailyMetric=WEBSITE_CLICKS", null),
            ("google-business-profile", "createPost") => (HttpMethod.Post, "https://mybusiness.googleapis.com/v4/" + Required(metadata, "locationId") + "/localPosts", args),
            ("outlook", "list") => (HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/messages?$top=10&$select=id,subject,from,receivedDateTime", null),
            ("outlook", "get") => (HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/messages/" + Required(args, "id"), null),
            ("outlook", "send") => (HttpMethod.Post, "https://graph.microsoft.com/v1.0/me/sendMail", BuildOutlookSend(args)),
            ("microsoft-calendar", "list") => (HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/events?$top=10&$select=id,subject,start,end,attendees", null),
            ("microsoft-calendar", "create") => (HttpMethod.Post, "https://graph.microsoft.com/v1.0/me/events", args),
            ("microsoft-calendar", "update") => (HttpMethod.Patch, "https://graph.microsoft.com/v1.0/me/events/" + Required(args, "eventId"), args),
            ("microsoft-calendar", "delete") => (HttpMethod.Delete, "https://graph.microsoft.com/v1.0/me/events/" + Required(args, "eventId"), null),
            ("microsoft-onedrive", "list") => (HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/drive/root/children?$top=20&$select=id,name,size,lastModifiedDateTime", null),
            ("microsoft-onedrive", "get") => (HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/drive/items/" + Required(args, "fileId") + "?$select=id,name,size,webUrl", null),
            ("microsoft-onedrive", "upload") => (HttpMethod.Put, "https://graph.microsoft.com/v1.0/me/drive/root:/" + Uri.EscapeDataString(Required(args, "fileName")) + ":/content", args),
            _ => (HttpMethod.Get, null, null)
        };
    }

    private static JsonObject BuildOutlookSend(JsonObject args) =>
        new()
        {
            ["message"] = new JsonObject
            {
                ["subject"] = Text(args, "subject") ?? "Message",
                ["body"] = new JsonObject
                {
                    ["contentType"] = "Text",
                    ["content"] = Text(args, "body") ?? Text(args, "text") ?? ""
                },
                ["toRecipients"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["emailAddress"] = new JsonObject { ["address"] = Text(args, "to") ?? "" }
                    }
                }
            },
            ["saveToSentItems"] = true
        };

    private static string ResolveGmailRaw(JsonObject args)
    {
        var raw = Text(args, "raw");
        if (!string.IsNullOrWhiteSpace(raw))
            return raw;
        var to = Text(args, "to") ?? "";
        var subject = Text(args, "subject") ?? "";
        var body = Text(args, "body") ?? Text(args, "text") ?? "";
        if (string.IsNullOrWhiteSpace(to))
            throw new InvalidOperationException("A valid to address or raw MIME payload is required.");
        return GmailMime.EncodeRawMessage(to, subject, body, Text(args, "from"), Text(args, "inReplyTo"), Text(args, "references"));
    }

    private static (HttpMethod Method, string? Url, JsonObject? Body) WhatsAppSendTemplate(JsonObject args, JsonObject metadata, string? phoneNumberId)
    {
        var to = Text(args, "to") ?? Text(args, "phone") ?? "";
        var template = Text(args, "templateName") ?? Text(args, "template") ?? "hello_world";
        var language = Text(args, "language") ?? "en_US";
        var body = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = to,
            ["type"] = "template",
            ["template"] = new JsonObject
            {
                ["name"] = template,
                ["language"] = new JsonObject { ["code"] = language }
            }
        };
        return (HttpMethod.Post, "https://graph.facebook.com/v21.0/" + (phoneNumberId ?? Required(args, "phoneNumberId")) + "/messages", body);
    }

    private static (HttpMethod Method, string? Url, JsonObject? Body) WhatsAppSendText(JsonObject args, JsonObject metadata, string? phoneNumberId)
    {
        var to = Text(args, "to") ?? Text(args, "phone") ?? "";
        var text = Text(args, "body") ?? Text(args, "text") ?? "";
        var body = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = to,
            ["type"] = "text",
            ["text"] = new JsonObject { ["body"] = text }
        };
        return (HttpMethod.Post, "https://graph.facebook.com/v21.0/" + (phoneNumberId ?? Required(args, "phoneNumberId")) + "/messages", body);
    }

    private static (HttpMethod Method, string? Url, JsonObject? Body) MetaPublish(JsonObject args, JsonObject metadata)
    {
        var message = Text(args, "message") ?? Text(args, "body") ?? Text(args, "caption") ?? "";
        var body = new JsonObject { ["message"] = message };
        var link = Text(args, "link");
        if (!string.IsNullOrWhiteSpace(link))
            body["link"] = link;
        return (HttpMethod.Post, "https://graph.facebook.com/v21.0/" + Required(metadata, "pageId") + "/feed", body);
    }

    private static (HttpMethod Method, string? Url, JsonObject? Body) InstagramPublish(JsonObject args, JsonObject metadata, string? igAccount)
    {
        var account = igAccount ?? Text(args, "instagramBusinessAccountId");
        if (string.IsNullOrWhiteSpace(account))
            return (HttpMethod.Post, null, null);
        var imageUrl = Text(args, "imageUrl") ?? Text(args, "mediaUrl");
        var caption = Text(args, "caption") ?? Text(args, "message") ?? "";
        if (string.IsNullOrWhiteSpace(imageUrl))
            return (HttpMethod.Post, null, null);
        // Container create; publish step is a separate follow-up when creation id is returned.
        var body = new JsonObject
        {
            ["image_url"] = imageUrl,
            ["caption"] = caption,
            ["access_token_placeholder"] = false
        };
        return (HttpMethod.Post, "https://graph.facebook.com/v21.0/" + Uri.EscapeDataString(account) + "/media", body);
    }

    private static (HttpMethod Method, string? Url, JsonObject? Body) MetaInsights(JsonObject args, JsonObject metadata)
    {
        var postId = Text(args, "externalPostId") ?? Text(args, "postId") ?? Text(args, "id");
        if (string.IsNullOrWhiteSpace(postId) || postId.Contains('/') || postId.Contains('\\') || postId.Contains(".."))
            return (HttpMethod.Get, null, null);
        var metrics = Text(args, "metrics") ?? "post_impressions,post_engaged_users,post_clicks";
        return (HttpMethod.Get, "https://graph.facebook.com/v21.0/" + Uri.EscapeDataString(postId) + "/insights?metric=" + Uri.EscapeDataString(metrics), null);
    }

    public static string? Text(JsonObject obj, string name) =>
        obj[name]?.GetValue<string>();

    public static string Required(JsonObject obj, string name)
    {
        var value = Text(obj, name);
        if (string.IsNullOrWhiteSpace(value) || value.Contains('/') || value.Contains('\\') || value.Contains(".."))
            throw new InvalidOperationException($"A valid {name} is required.");
        return Uri.EscapeDataString(value);
    }
}

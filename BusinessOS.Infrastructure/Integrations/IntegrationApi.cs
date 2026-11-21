using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;

namespace BusinessOS.Infrastructure.Integrations;

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

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (provider.Key == "github")
            request.Headers.UserAgent.ParseAdd("CompanyOS");
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        var redacted = NalvaGuard.Redact(NalvaGuard.Clip(text, 2000));
        if (!response.IsSuccessStatusCode)
        {
            var errorClass = (int)response.StatusCode == 429
                ? "rate_limit"
                : (int)response.StatusCode is 401 or 403 ? "permission" : "provider_error";
            return new IntegrationCallResult
            {
                Performed = false,
                Status = errorClass,
                Message = $"The provider returned {(int)response.StatusCode}.",
                Data = new JsonObject { ["statusCode"] = (int)response.StatusCode, ["body"] = redacted }
            };
        }

        JsonNode? payload = null;
        try
        {
            payload = JsonNode.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
        }
        catch (System.Text.Json.JsonException)
        {
            payload = new JsonObject { ["body"] = redacted };
        }

        return new IntegrationCallResult
        {
            Performed = true,
            Status = "completed",
            Message = "The provider accepted the request.",
            Data = new JsonObject
            {
                ["operation"] = operation,
                ["provider"] = provider.Key,
                ["response"] = payload
            }
        };
    }

    private static (HttpMethod Method, string? Url, JsonObject? Body) Build(string provider, string operation, JsonObject args, JsonObject metadata)
    {
        var shop = Text(metadata, "shop");
        var site = Text(metadata, "site");
        var property = Text(metadata, "propertyId");
        var customer = Text(metadata, "customerId");
        var phoneNumberId = Text(metadata, "phoneNumberId");
        return (provider, operation) switch
        {
            ("gmail", "list") => (HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults=10&q=" + Uri.EscapeDataString(Text(args, "query") ?? ""), null),
            ("gmail", "get") => (HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/messages/" + Required(args, "id"), null),
            ("gmail", "draft") => (HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/drafts", new JsonObject { ["message"] = new JsonObject { ["raw"] = Text(args, "raw") ?? "" } }),
            ("gmail", "send") => (HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send", new JsonObject { ["raw"] = Text(args, "raw") ?? "" }),
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
            ("whatsapp", "sendTemplate") => (HttpMethod.Post, "https://graph.facebook.com/v21.0/" + (phoneNumberId ?? Required(args, "phoneNumberId")) + "/messages", args),
            ("stripe", "balance") => (HttpMethod.Get, "https://api.stripe.com/v1/balance", null),
            ("stripe", "payment") => (HttpMethod.Get, "https://api.stripe.com/v1/payment_intents/" + Required(args, "paymentIntentId"), null),
            ("stripe", "refund") => (HttpMethod.Post, "https://api.stripe.com/v1/refunds", new JsonObject { ["payment_intent"] = Text(args, "paymentIntentId") ?? "" }),
            ("paypal", "order") => (HttpMethod.Get, "https://api-m.paypal.com/v2/checkout/orders/" + Required(args, "orderId"), null),
            ("paypal", "refund") => (HttpMethod.Post, "https://api-m.paypal.com/v2/payments/captures/" + Required(args, "captureId") + "/refund", new JsonObject()),
            ("linkedin", "organization") => (HttpMethod.Get, "https://api.linkedin.com/v2/organizationalEntityAcls?q=roleAssignee", null),
            ("meta", "pages") => (HttpMethod.Get, "https://graph.facebook.com/v21.0/me/accounts", null),
            ("meta", "insights") => MetaInsights(args, metadata),
            ("meta", "publish") => MetaPublish(args, metadata),
            ("google-analytics", "report") => (HttpMethod.Post, "https://analyticsdata.googleapis.com/v1beta/properties/" + (property ?? Required(args, "propertyId")) + ":runReport", args),
            ("google-ads", "report") => (HttpMethod.Post, "https://googleads.googleapis.com/v18/customers/" + (customer ?? Required(args, "customerId")) + "/googleAds:search", args),
            ("shopify", "products") => (HttpMethod.Get, $"https://{shop}.myshopify.com/admin/api/2024-10/products.json?limit=10", null),
            ("shopify", "orders") => (HttpMethod.Get, $"https://{shop}.myshopify.com/admin/api/2024-10/orders.json?limit=10&status=any", null),
            ("shopify", "updateProduct") => (HttpMethod.Put, $"https://{shop}.myshopify.com/admin/api/2024-10/products/{Required(args, "productId")}.json", args),
            ("wordpress", "posts") => (HttpMethod.Get, "https://public-api.wordpress.com/rest/v1.1/sites/" + Uri.EscapeDataString(site ?? Required(args, "site")) + "/posts/?number=10", null),
            ("wordpress", "draft") => (HttpMethod.Post, "https://public-api.wordpress.com/rest/v1.1/sites/" + Uri.EscapeDataString(site ?? Required(args, "site")) + "/posts/new", args),
            _ => (HttpMethod.Get, null, null)
        };
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

    private static (HttpMethod Method, string? Url, JsonObject? Body) MetaInsights(JsonObject args, JsonObject metadata)
    {
        var postId = Text(args, "externalPostId") ?? Text(args, "postId") ?? Text(args, "id");
        if (string.IsNullOrWhiteSpace(postId) || postId.Contains('/') || postId.Contains('\\') || postId.Contains(".."))
            return (HttpMethod.Get, null, null);
        var metrics = Text(args, "metrics") ?? "post_impressions,post_engaged_users,post_clicks";
        return (HttpMethod.Get, "https://graph.facebook.com/v21.0/" + Uri.EscapeDataString(postId) + "/insights?metric=" + Uri.EscapeDataString(metrics), null);
    }

    private static string? Text(JsonObject obj, string name) =>
        obj[name]?.GetValue<string>();

    private static string Required(JsonObject obj, string name)
    {
        var value = Text(obj, name);
        if (string.IsNullOrWhiteSpace(value) || value.Contains('/') || value.Contains('\\') || value.Contains(".."))
            throw new InvalidOperationException($"A valid {name} is required.");
        return Uri.EscapeDataString(value);
    }
}

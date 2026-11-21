using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.SalesAgent;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Integrations.Providers;

public sealed class GmailIntegrationProvider : CatalogIntegrationProvider
{
    private readonly BusinessOSDbContext _db;

    public GmailIntegrationProvider(BusinessOSDbContext db) : base("gmail") => _db = db;

    public override async Task<IntegrationCallResult> ExecuteAsync(
        HttpClient http,
        string operation,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(operation, "syncInbound", StringComparison.OrdinalIgnoreCase))
            return await SyncInboundAsync(http, accessToken, argumentsJson, metadataJson, cancellationToken);

        return await base.ExecuteAsync(http, operation, accessToken, argumentsJson, metadataJson, cancellationToken);
    }

    private async Task<IntegrationCallResult> SyncInboundAsync(
        HttpClient http,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken)
    {
        var args = JsonNode.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson) as JsonObject ?? new JsonObject();
        var query = Text(args, "query") ?? "is:unread newer_than:7d";
        var max = int.TryParse(Text(args, "maxResults"), out var n) ? Math.Clamp(n, 1, 25) : 10;
        var list = await IntegrationApi.ExecuteAsync(
            http,
            IntegrationProviderCatalog.All.First(x => x.Key == "gmail"),
            "list",
            accessToken,
            new JsonObject { ["query"] = query, ["maxResults"] = max.ToString() }.ToJsonString(),
            metadataJson,
            cancellationToken);

        if (!list.Performed)
            return list;

        var messages = list.Data["response"]?["messages"] as JsonArray ?? [];
        var synced = 0;
        var supportOpened = 0;
        var salesMatched = 0;

        foreach (var item in messages)
        {
            var id = item?["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var detail = await IntegrationApi.ExecuteAsync(
                http,
                IntegrationProviderCatalog.All.First(x => x.Key == "gmail"),
                "get",
                accessToken,
                new JsonObject { ["id"] = id }.ToJsonString(),
                metadataJson,
                cancellationToken);
            if (!detail.Performed)
                continue;

            var payload = detail.Data["response"] as JsonObject;
            var threadId = payload?["threadId"]?.GetValue<string>() ?? id;
            var snippet = payload?["snippet"]?.GetValue<string>() ?? "";
            var headers = payload?["payload"]?["headers"] as JsonArray;
            var from = Header(headers, "From") ?? "";
            var subject = Header(headers, "Subject") ?? "(no subject)";
            var messageIdHeader = Header(headers, "Message-ID") ?? id;

            var conversation = await _db.IntegrationConversations
                .FirstOrDefaultAsync(x => x.ProviderKey == "gmail" && x.ExternalThreadId == threadId, cancellationToken);
            if (conversation is null)
            {
                conversation = new IntegrationConversation
                {
                    ProviderKey = "gmail",
                    ExternalThreadId = threadId,
                    CustomerExternalId = from,
                    CustomerDisplay = from,
                    Channel = "email",
                    LastInboundAt = DateTime.UtcNow
                };
                _db.IntegrationConversations.Add(conversation);
                await _db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                conversation.LastInboundAt = DateTime.UtcNow;
            }

            var idempotency = "gmail:" + messageIdHeader;
            if (await _db.IntegrationConversationMessages.AsNoTracking()
                    .AnyAsync(x => x.IdempotencyKey == idempotency, cancellationToken))
                continue;

            _db.IntegrationConversationMessages.Add(new IntegrationConversationMessage
            {
                ConversationId = conversation.Id,
                Direction = IntegrationMessageDirections.Inbound,
                MessageType = "email",
                ExternalMessageId = id,
                Body = NalvaGuard.Clip(snippet, 4000),
                DeliveryStatus = "Received",
                IdempotencyKey = idempotency
            });

            var fromEmail = ExtractEmail(from);
            if (!string.IsNullOrWhiteSpace(fromEmail))
            {
                var outreach = await _db.SalesOutreachMessages
                    .Where(x => x.ToEmail == fromEmail && x.Status == SalesAgentStatuses.Sent)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (outreach is not null)
                {
                    outreach.Status = SalesAgentStatuses.Replied;
                    outreach.ResponseAt = DateTime.UtcNow;
                    outreach.ResponseSummary = NalvaGuard.Clip(snippet, 1000);
                    salesMatched++;
                }
                else
                {
                    supportOpened++;
                }
            }

            synced++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new IntegrationCallResult
        {
            Performed = true,
            Status = "completed",
            Message = $"Synced {synced} inbound Gmail message(s).",
            Data = new JsonObject
            {
                ["synced"] = synced,
                ["salesMatched"] = salesMatched,
                ["supportCandidates"] = supportOpened,
                ["provider"] = "gmail",
                ["operation"] = "syncInbound"
            }
        };
    }

    private static string? Header(JsonArray? headers, string name)
    {
        if (headers is null)
            return null;
        foreach (var header in headers)
        {
            if (string.Equals(header?["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase))
                return header?["value"]?.GetValue<string>();
        }
        return null;
    }

    private static string? ExtractEmail(string from)
    {
        var start = from.IndexOf('<');
        var end = from.IndexOf('>');
        if (start >= 0 && end > start)
            return from[(start + 1)..end].Trim().ToLowerInvariant();
        return from.Contains('@') ? from.Trim().ToLowerInvariant() : null;
    }

    private static string? Text(JsonObject obj, string name) => obj[name]?.GetValue<string>();
}

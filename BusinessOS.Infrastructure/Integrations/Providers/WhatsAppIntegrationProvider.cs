using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Application.Features.Support;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Infrastructure.Integrations.Providers;

public sealed class WhatsAppIntegrationProvider : CatalogIntegrationProvider
{
    private static readonly Regex Digits = new(@"\D", RegexOptions.Compiled);

    private readonly BusinessOSDbContext _db;
    private readonly IServiceScopeFactory? _scopes;

    public WhatsAppIntegrationProvider(BusinessOSDbContext db, IServiceScopeFactory? scopes = null) : base("whatsapp")
    {
        _db = db;
        _scopes = scopes;
    }

    public override async Task<IntegrationCallResult> ExecuteAsync(
        HttpClient http,
        string operation,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(operation, "conversations", StringComparison.OrdinalIgnoreCase))
            return await ListConversationsAsync(cancellationToken);
        if (string.Equals(operation, "messages", StringComparison.OrdinalIgnoreCase))
            return await ListMessagesAsync(argumentsJson, cancellationToken);
        if (string.Equals(operation, "sendText", StringComparison.OrdinalIgnoreCase))
            return await SendTextWithPolicyAsync(http, accessToken, argumentsJson, metadataJson, cancellationToken);

        var result = await base.ExecuteAsync(http, operation, accessToken, argumentsJson, metadataJson, cancellationToken);
        if (result.Performed && string.Equals(operation, "sendTemplate", StringComparison.OrdinalIgnoreCase))
            await RecordOutboundAsync(argumentsJson, "template", cancellationToken);
        return result;
    }

    public override async Task<IntegrationWebhookHandleResult?> HandleWebhookAsync(
        Guid tenantId,
        Guid connectionId,
        string body,
        string eventId,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (System.Text.Json.JsonException)
        {
            return new IntegrationWebhookHandleResult(false, "WhatsApp webhook body was not valid JSON.");
        }

        var entries = root?["entry"] as JsonArray ?? [];
        var stored = 0;
        foreach (var entry in entries)
        {
            var changes = entry?["changes"] as JsonArray ?? [];
            foreach (var change in changes)
            {
                var value = change?["value"] as JsonObject;
                var messages = value?["messages"] as JsonArray ?? [];
                var contacts = value?["contacts"] as JsonArray ?? [];
                var display = contacts.FirstOrDefault()?["profile"]?["name"]?.GetValue<string>();
                foreach (var message in messages)
                {
                    var from = message?["from"]?.GetValue<string>();
                    var id = message?["id"]?.GetValue<string>();
                    var text = message?["text"]?["body"]?.GetValue<string>()
                               ?? message?["button"]?["text"]?.GetValue<string>()
                               ?? "";
                    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(id))
                        continue;

                    var conversation = await _db.IntegrationConversations
                        .FirstOrDefaultAsync(x => x.ProviderKey == "whatsapp" && x.ExternalThreadId == from, cancellationToken);
                    if (conversation is null)
                    {
                        conversation = new IntegrationConversation
                        {
                            ProviderKey = "whatsapp",
                            ConnectionId = connectionId,
                            ExternalThreadId = from,
                            CustomerExternalId = from,
                            CustomerDisplay = display ?? from,
                            Channel = "whatsapp",
                            LastInboundAt = DateTime.UtcNow,
                            CustomerCareWindowEndsAt = DateTime.UtcNow.AddHours(24)
                        };
                        _db.IntegrationConversations.Add(conversation);
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        conversation.LastInboundAt = DateTime.UtcNow;
                        conversation.CustomerCareWindowEndsAt = DateTime.UtcNow.AddHours(24);
                        conversation.CustomerDisplay ??= display;
                    }

                    var key = "whatsapp:" + id;
                    if (await _db.IntegrationConversationMessages.AsNoTracking()
                            .AnyAsync(x => x.IdempotencyKey == key, cancellationToken))
                        continue;

                    var customer = await FindCustomerByPhoneAsync(from, cancellationToken);
                    if (customer is not null)
                    {
                        conversation.CustomerDisplay = $"{customer.FirstName} {customer.LastName}".Trim();
                        var meta = string.IsNullOrWhiteSpace(conversation.MetadataJson)
                            ? new JsonObject()
                            : JsonNode.Parse(conversation.MetadataJson) as JsonObject ?? new JsonObject();
                        meta["customerId"] = customer.Id.ToString();
                        conversation.MetadataJson = meta.ToJsonString();
                    }

                    _db.IntegrationConversationMessages.Add(new IntegrationConversationMessage
                    {
                        ConversationId = conversation.Id,
                        Direction = IntegrationMessageDirections.Inbound,
                        MessageType = message?["type"]?.GetValue<string>() ?? "text",
                        ExternalMessageId = id,
                        Body = NalvaGuard.Clip(text, 4000),
                        DeliveryStatus = "Received",
                        IdempotencyKey = key
                    });
                    stored++;

                    await RouteToSupportAsync(
                        tenantId,
                        from,
                        id,
                        text,
                        customer?.Id,
                        customer?.PhoneNumber,
                        cancellationToken);
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new IntegrationWebhookHandleResult(true, $"Stored {stored} WhatsApp inbound message(s).", new JsonObject { ["stored"] = stored });
    }

    private async Task<Customer?> FindCustomerByPhoneAsync(string phone, CancellationToken cancellationToken)
    {
        var normalized = Digits.Replace(phone ?? "", "");
        if (normalized.Length < 7)
            return null;
        var customers = await _db.Customers.AsNoTracking()
            .Where(x => x.IsActive && x.PhoneNumber != null && x.PhoneNumber != "")
            .Take(500)
            .ToListAsync(cancellationToken);
        return customers.FirstOrDefault(c =>
        {
            var candidate = Digits.Replace(c.PhoneNumber, "");
            return candidate.EndsWith(normalized, StringComparison.Ordinal)
                   || normalized.EndsWith(candidate, StringComparison.Ordinal);
        });
    }

    private async Task RouteToSupportAsync(
        Guid tenantId,
        string phone,
        string messageId,
        string body,
        Guid? customerId,
        string? customerPhone,
        CancellationToken cancellationToken)
    {
        if (_scopes is null || string.IsNullOrWhiteSpace(body))
            return;

        try
        {
            using var scope = _scopes.CreateScope();
            var tenant = scope.ServiceProvider.GetRequiredService<BusinessOS.Application.Common.Interfaces.ITenantProvider>();
            tenant.SetTenantId(tenantId);
            var support = scope.ServiceProvider.GetService<ISupportAgentService>();
            if (support is null)
                return;

            await support.ReceiveInboundAsync(new SupportInboundMessageRequest
            {
                Channel = SupportChannels.WhatsApp,
                Body = body,
                Subject = "WhatsApp inbound",
                ExternalThreadId = phone,
                ExternalMessageId = messageId,
                IdempotencyKey = "whatsapp-support:" + messageId,
                CustomerId = customerId,
                CustomerPhone = customerPhone ?? phone,
                ProcessWithAgent = true
            }, cancellationToken);
        }
        catch
        {
            // Webhook storage already succeeded; support routing failures must not fail signature-accepted delivery.
        }
    }

    private async Task<IntegrationCallResult> SendTextWithPolicyAsync(
        HttpClient http,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken)
    {
        var args = JsonNode.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson) as JsonObject ?? new JsonObject();
        var to = args["to"]?.GetValue<string>() ?? args["phone"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(to))
        {
            return new IntegrationCallResult
            {
                Performed = false,
                Status = "validation",
                Message = "A WhatsApp recipient phone number is required."
            };
        }

        var conversation = await _db.IntegrationConversations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderKey == "whatsapp" && x.ExternalThreadId == to, cancellationToken);
        if (conversation?.CustomerCareWindowEndsAt is not DateTime ends || ends < DateTime.UtcNow)
        {
            return new IntegrationCallResult
            {
                Performed = false,
                Status = "policy",
                Message = "Free-form WhatsApp text is only allowed inside the 24-hour customer care window. Use an approved template instead."
            };
        }

        var result = await base.ExecuteAsync(http, "sendText", accessToken, argumentsJson, metadataJson, cancellationToken);
        if (result.Performed)
            await RecordOutboundAsync(argumentsJson, "text", cancellationToken);
        return result;
    }

    private async Task RecordOutboundAsync(string argumentsJson, string type, CancellationToken cancellationToken)
    {
        var args = JsonNode.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson) as JsonObject ?? new JsonObject();
        var to = args["to"]?.GetValue<string>() ?? args["phone"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(to))
            return;

        var conversation = await _db.IntegrationConversations
            .FirstOrDefaultAsync(x => x.ProviderKey == "whatsapp" && x.ExternalThreadId == to, cancellationToken);
        if (conversation is null)
        {
            conversation = new IntegrationConversation
            {
                ProviderKey = "whatsapp",
                ExternalThreadId = to,
                CustomerExternalId = to,
                CustomerDisplay = to,
                Channel = "whatsapp",
                LastOutboundAt = DateTime.UtcNow
            };
            _db.IntegrationConversations.Add(conversation);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            conversation.LastOutboundAt = DateTime.UtcNow;
        }

        _db.IntegrationConversationMessages.Add(new IntegrationConversationMessage
        {
            ConversationId = conversation.Id,
            Direction = IntegrationMessageDirections.Outbound,
            MessageType = type,
            Body = NalvaGuard.Clip(args["body"]?.GetValue<string>() ?? args["text"]?.GetValue<string>() ?? args["templateName"]?.GetValue<string>() ?? "", 4000),
            TemplateName = args["templateName"]?.GetValue<string>() ?? args["template"]?.GetValue<string>(),
            RequiresApproval = true,
            Approved = true,
            DeliveryStatus = "Sent",
            IdempotencyKey = args["idempotencyKey"]?.GetValue<string>()
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IntegrationCallResult> ListConversationsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.IntegrationConversations.AsNoTracking()
            .Where(x => x.ProviderKey == "whatsapp")
            .OrderByDescending(x => x.LastInboundAt ?? x.LastOutboundAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var array = new JsonArray();
        foreach (var x in rows)
        {
            array.Add(new JsonObject
            {
                ["id"] = x.Id.ToString(),
                ["externalThreadId"] = x.ExternalThreadId,
                ["customerDisplay"] = x.CustomerDisplay,
                ["lastInboundAt"] = x.LastInboundAt,
                ["customerCareWindowEndsAt"] = x.CustomerCareWindowEndsAt
            });
        }

        return new IntegrationCallResult
        {
            Performed = true,
            Status = "completed",
            Message = $"Found {rows.Count} WhatsApp conversation(s).",
            Data = new JsonObject { ["conversations"] = array }
        };
    }

    private async Task<IntegrationCallResult> ListMessagesAsync(string argumentsJson, CancellationToken cancellationToken)
    {
        var args = JsonNode.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson) as JsonObject ?? new JsonObject();
        var thread = args["threadId"]?.GetValue<string>() ?? args["to"]?.GetValue<string>();
        var conversation = await _db.IntegrationConversations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderKey == "whatsapp" && (x.ExternalThreadId == thread || x.Id.ToString() == thread), cancellationToken);
        if (conversation is null)
        {
            return new IntegrationCallResult
            {
                Performed = false,
                Status = "not_found",
                Message = "No WhatsApp conversation was found for that thread."
            };
        }

        var messages = await _db.IntegrationConversationMessages.AsNoTracking()
            .Where(x => x.ConversationId == conversation.Id)
            .OrderBy(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var array = new JsonArray();
        foreach (var x in messages)
        {
            array.Add(new JsonObject
            {
                ["id"] = x.Id.ToString(),
                ["direction"] = x.Direction,
                ["body"] = x.Body,
                ["messageType"] = x.MessageType,
                ["deliveryStatus"] = x.DeliveryStatus,
                ["createdAt"] = x.CreatedAt
            });
        }

        return new IntegrationCallResult
        {
            Performed = true,
            Status = "completed",
            Message = $"Found {messages.Count} message(s).",
            Data = new JsonObject { ["messages"] = array }
        };
    }
}

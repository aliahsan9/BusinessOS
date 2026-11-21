using System.Text.Json.Nodes;
using BusinessOS.Application.Features.Integrations;
using BusinessOS.Application.Features.Nalva;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Integrations.Providers;

/// <summary>
/// Company Stripe Connect integration (tenant's customers). Separate from SaaS subscription billing.
/// Never handles raw card data.
/// </summary>
public sealed class StripeConnectIntegrationProvider : CatalogIntegrationProvider
{
    private readonly BusinessOSDbContext _db;

    public StripeConnectIntegrationProvider(BusinessOSDbContext db) : base("stripe") => _db = db;

    public override async Task<IntegrationCallResult> ExecuteAsync(
        HttpClient http,
        string operation,
        string accessToken,
        string argumentsJson,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        var result = await base.ExecuteAsync(http, operation, accessToken, argumentsJson, metadataJson, cancellationToken);
        if (!result.Performed)
            return result;

        if (string.Equals(operation, "payment", StringComparison.OrdinalIgnoreCase))
            await UpsertFromPaymentIntentAsync(result.Data["response"] as JsonObject, null, cancellationToken);
        else if (string.Equals(operation, "payments", StringComparison.OrdinalIgnoreCase))
        {
            var data = result.Data["response"]?["data"] as JsonArray ?? [];
            foreach (var item in data)
                await UpsertFromPaymentIntentAsync(item as JsonObject, null, cancellationToken);
        }

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
            return new IntegrationWebhookHandleResult(false, "Stripe webhook body was not valid JSON.");
        }

        var type = root?["type"]?.GetValue<string>() ?? "";
        var obj = root?["data"]?["object"] as JsonObject;
        if (obj is null)
            return new IntegrationWebhookHandleResult(true, "Stripe event acknowledged without payment object.");

        if (type.StartsWith("payment_intent.", StringComparison.Ordinal) || type.StartsWith("charge.", StringComparison.Ordinal))
        {
            await UpsertFromPaymentIntentAsync(obj, eventId, cancellationToken, connectionId);
            return new IntegrationWebhookHandleResult(true, "Company payment status updated.", new JsonObject
            {
                ["type"] = type,
                ["paymentId"] = obj["id"]?.GetValue<string>()
            });
        }

        return new IntegrationWebhookHandleResult(true, "Stripe event acknowledged.");
    }

    private async Task UpsertFromPaymentIntentAsync(JsonObject? obj, string? eventId, CancellationToken cancellationToken, Guid? connectionId = null)
    {
        if (obj is null)
            return;
        var externalId = obj["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(externalId))
            return;

        var row = await _db.CompanyPaymentRecords
            .FirstOrDefaultAsync(x => x.ProviderKey == "stripe" && x.ExternalPaymentId == externalId, cancellationToken);
        if (row is null)
        {
            row = new CompanyPaymentRecord
            {
                ProviderKey = "stripe",
                ExternalPaymentId = externalId,
                ConnectionId = connectionId,
                IdempotencyKey = "stripe:" + externalId
            };
            _db.CompanyPaymentRecords.Add(row);
        }

        row.Status = obj["status"]?.GetValue<string>() ?? row.Status;
        row.AmountMinor = obj["amount"]?.GetValue<long?>() ?? obj["amount_received"]?.GetValue<long?>() ?? row.AmountMinor;
        row.Currency = obj["currency"]?.GetValue<string>() ?? row.Currency;
        row.ExternalCustomerId = obj["customer"]?.GetValue<string>() ?? row.ExternalCustomerId;
        row.LastEventId = eventId ?? row.LastEventId;
        row.MetadataJson = NalvaGuard.Clip(obj.ToJsonString(), 4000);
        if (string.Equals(row.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            row.PaidAt ??= DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}

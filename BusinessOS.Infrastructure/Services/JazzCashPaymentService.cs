using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Idempotency;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

public sealed class JazzCashPaymentService : IJazzCashPaymentService
{
    private readonly JazzCashOptions _options;
    private readonly IWebhookEventStore _events;
    private readonly IBillingPaymentReconciler _reconciler;
    private readonly ILogger<JazzCashPaymentService> _logger;

    public JazzCashPaymentService(
        IOptions<JazzCashOptions> options,
        IWebhookEventStore events,
        IBillingPaymentReconciler reconciler,
        ILogger<JazzCashPaymentService> logger)
    {
        _options = options.Value;
        _events = events;
        _reconciler = reconciler;
        _logger = logger;
    }

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.MerchantId);

    public Task<CheckoutSessionDto> InitiatePaymentAsync(
        Guid tenantId,
        SubscriptionPlanDto plan,
        string billingInterval,
        CancellationToken cancellationToken = default,
        string? operationKey = null)
    {
        var isYearly = billingInterval.Equals("yearly", StringComparison.OrdinalIgnoreCase);
        var amount = isYearly ? plan.AnnualPrice : plan.MonthlyPrice;
        var txnRef = string.IsNullOrWhiteSpace(operationKey)
            ? $"JC-{tenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : operationKey;
        var txnDateTime = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        var payload = new Dictionary<string, string>
        {
            ["pp_Version"] = "1.1",
            ["pp_TxnType"] = "MWALLET",
            ["pp_Language"] = "EN",
            ["pp_MerchantID"] = _options.MerchantId,
            ["pp_SubMerchantID"] = string.Empty,
            ["pp_Password"] = _options.Password,
            ["pp_BankID"] = "TBANK",
            ["pp_ProductID"] = "RETL",
            ["pp_TxnRefNo"] = txnRef,
            ["pp_Amount"] = ((long)(amount * 100)).ToString(),
            ["pp_TxnCurrency"] = "PKR",
            ["pp_TxnDateTime"] = txnDateTime,
            ["pp_BillReference"] = plan.Slug,
            ["pp_Description"] = $"BusinessOS {plan.Name} Subscription",
            ["pp_TxnExpiryDateTime"] = DateTime.UtcNow.AddHours(1).ToString("yyyyMMddHHmmss"),
            ["pp_ReturnURL"] = _options.ReturnUrl,
            ["ppmpf_1"] = tenantId.ToString(),
            ["ppmpf_2"] = plan.Id.ToString(),
            ["ppmpf_3"] = billingInterval
        };
        payload["pp_SecureHash"] = JazzCashSecureHash.Compute(payload, _options.IntegritySalt);

        _logger.LogInformation("JazzCash payment initiated: {TxnRef} for tenant {TenantId}", txnRef, tenantId);

        return Task.FromResult(new CheckoutSessionDto
        {
            SessionId = txnRef,
            CheckoutUrl = _options.PostUrl,
            Provider = "jazzcash"
        });
    }

    public Task<bool> VerifyPaymentAsync(string transactionReference, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "JazzCash server-side confirmation is not enabled for {Reference}. Payment state was not changed.",
            transactionReference);
        return Task.FromResult(false);
    }

    public async Task HandleWebhookAsync(string payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("JazzCash callback received: {Length} bytes", payload?.Length ?? 0);

        if (!IsConfigured || string.IsNullOrWhiteSpace(_options.IntegritySalt))
        {
            _logger.LogWarning("JazzCash callback rejected because integrity verification is not configured.");
            throw new UnauthorizedException("Payment callback was rejected.");
        }

        var data = ParseFields(payload);
        if (!data.TryGetValue("pp_SecureHash", out var presented) ||
            !JazzCashSecureHash.Matches(data, _options.IntegritySalt, presented))
        {
            _logger.LogWarning("JazzCash callback rejected because the secure hash did not match.");
            throw new UnauthorizedException("Payment callback was rejected.");
        }

        if (!data.TryGetValue("pp_MerchantID", out var merchantId) ||
            !string.Equals(merchantId, _options.MerchantId, StringComparison.Ordinal))
        {
            _logger.LogWarning("JazzCash callback rejected because the merchant id did not match.");
            throw new UnauthorizedException("Payment callback was rejected.");
        }

        _logger.LogInformation(
            "JazzCash callback signature accepted for {TxnRef}",
            data.GetValueOrDefault("pp_TxnRefNo"));

        var txnRef = data.GetValueOrDefault("pp_TxnRefNo");
        var responseCode = data.GetValueOrDefault("pp_ResponseCode");
        if (string.IsNullOrWhiteSpace(txnRef))
            throw new BadRequestException("JazzCash callback is missing a transaction reference.");

        var eventId = $"{txnRef}|{responseCode}|{data.GetValueOrDefault("pp_RetreivalReferenceNo")}";
        var claim = await _events.TryBeginAsync(PaymentProviderType.JazzCash, eventId, cancellationToken);
        if (claim is WebhookClaim.Duplicate)
            return;

        if (claim is WebhookClaim.Busy)
        {
            throw new ConflictException(
                "Webhook event is already being processed.",
                ErrorCodes.IdempotencyInProgress);
        }

        var started = (WebhookClaim.Started)claim;
        try
        {
            if (responseCode is "000" or "121" or "200")
            {
                var amount = ParseMinorAmount(data.GetValueOrDefault("pp_Amount"));
                var tenantId = await _reconciler.ApplyVerifiedResultAsync(
                    PaymentProviderType.JazzCash,
                    txnRef,
                    BillingTransactionStatus.Completed,
                    data.GetValueOrDefault("pp_RetreivalReferenceNo"),
                    amount,
                    "PKR",
                    cancellationToken);

                if (tenantId is null)
                {
                    await _events.AbandonAsync(started.EventRecordId, cancellationToken);
                    throw new ConflictException("Payment operation was not found for this callback.");
                }

                await _events.CompleteAsync(started.EventRecordId, tenantId, cancellationToken);
                return;
            }

            if (responseCode is not ("124" or "157"))
            {
                await _reconciler.ApplyVerifiedResultAsync(
                    PaymentProviderType.JazzCash,
                    txnRef,
                    BillingTransactionStatus.Failed,
                    data.GetValueOrDefault("pp_RetreivalReferenceNo"),
                    null,
                    "PKR",
                    cancellationToken);
            }

            await _events.CompleteAsync(started.EventRecordId, null, cancellationToken);
        }
        catch (ConflictException)
        {
            throw;
        }
        catch
        {
            await _events.AbandonAsync(started.EventRecordId, cancellationToken);
            throw;
        }
    }

    private static decimal? ParseMinorAmount(string? minor)
    {
        if (!long.TryParse(minor, out var value))
            return null;

        return value / 100m;
    }

    private static Dictionary<string, string> ParseFields(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(payload);
            if (parsed is not null)
            {
                return new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (JsonException)
        {
            // Form-encoded callbacks are parsed below.
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in payload.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
            fields[key] = value;
        }

        return fields;
    }
}

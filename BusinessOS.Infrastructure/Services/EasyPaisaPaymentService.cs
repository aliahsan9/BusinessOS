using System.Security.Cryptography;
using System.Text;
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

public sealed class EasyPaisaPaymentService : IEasyPaisaPaymentService
{
    private readonly EasyPaisaOptions _options;
    private readonly IWebhookEventStore _events;
    private readonly IBillingPaymentReconciler _reconciler;
    private readonly ILogger<EasyPaisaPaymentService> _logger;

    public EasyPaisaPaymentService(
        IOptions<EasyPaisaOptions> options,
        IWebhookEventStore events,
        IBillingPaymentReconciler reconciler,
        ILogger<EasyPaisaPaymentService> logger)
    {
        _options = options.Value;
        _events = events;
        _reconciler = reconciler;
        _logger = logger;
    }

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.StoreId);

    public Task<CheckoutSessionDto> InitiatePaymentAsync(
        Guid tenantId,
        SubscriptionPlanDto plan,
        string billingInterval,
        CancellationToken cancellationToken = default,
        string? operationKey = null)
    {
        var isYearly = billingInterval.Equals("yearly", StringComparison.OrdinalIgnoreCase);
        var amount = isYearly ? plan.AnnualPrice : plan.MonthlyPrice;
        var orderId = string.IsNullOrWhiteSpace(operationKey)
            ? $"EP-{tenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : operationKey;

        var payload = new Dictionary<string, string>
        {
            ["storeId"] = _options.StoreId,
            ["orderId"] = orderId,
            ["transactionAmount"] = amount.ToString("F2"),
            ["transactionType"] = "MA",
            ["mobileAccountNo"] = string.Empty,
            ["emailAddress"] = string.Empty,
            ["tokenExpiry"] = DateTime.UtcNow.AddHours(1).ToString("yyyyMMdd HHmmss"),
            ["bankIdentificationNumber"] = string.Empty,
            ["merchantHashedReq"] = ComputeHash(orderId, amount),
            ["returnUrl"] = _options.ReturnUrl,
            ["tenantId"] = tenantId.ToString(),
            ["planId"] = plan.Id.ToString(),
            ["billingInterval"] = billingInterval
        };

        _logger.LogInformation("EasyPaisa payment initiated: {OrderId} for tenant {TenantId}", orderId, tenantId);

        return Task.FromResult(new CheckoutSessionDto
        {
            SessionId = orderId,
            CheckoutUrl = _options.PostUrl,
            Provider = "easypaisa"
        });
    }

    public Task<bool> VerifyPaymentAsync(string transactionReference, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EasyPaisa server-side confirmation is not enabled for {Reference}. Payment state was not changed.",
            transactionReference);
        return Task.FromResult(false);
    }

    public async Task HandleWebhookAsync(string payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EasyPaisa callback received: {Length} bytes", payload?.Length ?? 0);

        if (!IsConfigured || string.IsNullOrWhiteSpace(_options.HashKey))
        {
            _logger.LogWarning("EasyPaisa callback rejected because hash verification is not configured.");
            throw new UnauthorizedException("Payment callback was rejected.");
        }

        var data = ParseFields(payload);
        if (!data.TryGetValue("orderId", out var orderId) ||
            !data.TryGetValue("transactionAmount", out var amountText) ||
            !data.TryGetValue("merchantHashedReq", out var presented) ||
            !decimal.TryParse(amountText, out var amount))
        {
            throw new UnauthorizedException("Payment callback was rejected.");
        }

        var expected = ComputeHash(orderId, amount);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        if (expectedBytes.Length != presentedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes))
        {
            _logger.LogWarning("EasyPaisa callback rejected because the hash did not match.");
            throw new UnauthorizedException("Payment callback was rejected.");
        }

        var statusText = data.GetValueOrDefault("transactionStatus") ?? data.GetValueOrDefault("status");
        var eventId = $"{orderId}|{statusText}";
        var claim = await _events.TryBeginAsync(PaymentProviderType.EasyPaisa, eventId, cancellationToken);
        if (claim is WebhookClaim.Duplicate)
            return;

        if (claim is WebhookClaim.Busy)
        {
            throw new ConflictException(
                "Webhook event is already being processed.",
                ErrorCodes.IdempotencyInProgress);
        }

        var started = (WebhookClaim.Started)claim;
        var success = statusText is not null &&
            (statusText.Equals("paid", StringComparison.OrdinalIgnoreCase) ||
             statusText.Equals("success", StringComparison.OrdinalIgnoreCase) ||
             statusText == "000");

        try
        {
            var tenantId = await _reconciler.ApplyVerifiedResultAsync(
                PaymentProviderType.EasyPaisa,
                orderId,
                success ? BillingTransactionStatus.Completed : BillingTransactionStatus.Failed,
                data.GetValueOrDefault("transactionId"),
                amount,
                "PKR",
                cancellationToken);

            if (tenantId is null && success)
            {
                await _events.AbandonAsync(started.EventRecordId, cancellationToken);
                throw new ConflictException("Payment operation was not found for this callback.");
            }

            await _events.CompleteAsync(started.EventRecordId, tenantId, cancellationToken);
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

    private static Dictionary<string, string> ParseFields(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(payload);
            if (parsed is not null)
                return new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
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
                continue;

            fields[Uri.UnescapeDataString(pair[..separator])] = Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        return fields;
    }

    private string ComputeHash(string orderId, decimal amount)
    {
        var input = $"{_options.StoreId}{orderId}{amount:F2}{_options.HashKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }
}

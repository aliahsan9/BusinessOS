using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Infrastructure.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

public sealed class JazzCashPaymentService : IJazzCashPaymentService
{
    private readonly JazzCashOptions _options;
    private readonly ILogger<JazzCashPaymentService> _logger;

    public JazzCashPaymentService(
        IOptions<JazzCashOptions> options,
        ILogger<JazzCashPaymentService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.MerchantId);

    public Task<CheckoutSessionDto> InitiatePaymentAsync(
        Guid tenantId,
        SubscriptionPlanDto plan,
        string billingInterval,
        CancellationToken cancellationToken = default)
    {
        var isYearly = billingInterval.Equals("yearly", StringComparison.OrdinalIgnoreCase);
        var amount = isYearly ? plan.AnnualPrice : plan.MonthlyPrice;
        var txnRef = $"JC-{tenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}";
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

    public Task HandleWebhookAsync(string payload, CancellationToken cancellationToken = default)
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
            "JazzCash callback signature accepted for {TxnRef}. Payment state was not changed.",
            data.GetValueOrDefault("pp_TxnRefNo"));
        return Task.CompletedTask;
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

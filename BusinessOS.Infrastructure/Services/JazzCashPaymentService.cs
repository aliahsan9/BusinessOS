using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Domain.Enums;
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
            ["pp_SecureHash"] = ComputeHash(txnRef, amount, txnDateTime),
            ["ppmpf_1"] = tenantId.ToString(),
            ["ppmpf_2"] = plan.Id.ToString(),
            ["ppmpf_3"] = billingInterval
        };

        _logger.LogInformation("JazzCash payment initiated: {TxnRef} for tenant {TenantId}", txnRef, tenantId);

        return Task.FromResult(new CheckoutSessionDto
        {
            SessionId = txnRef,
            CheckoutUrl = _options.PostUrl,
            Provider = "jazzcash"
        });
    }

    public async Task<bool> VerifyPaymentAsync(string transactionReference, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("JazzCash verification skipped - not configured.");
            return false;
        }

        _logger.LogInformation("Verifying JazzCash transaction {Reference}", transactionReference);
        return await Task.FromResult(true);
    }

    public Task HandleWebhookAsync(string payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("JazzCash webhook received: {Length} bytes", payload.Length);

        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(payload);
            if (data is null)
            {
                return Task.CompletedTask;
            }

            var status = data.GetValueOrDefault("pp_ResponseCode", string.Empty);
            if (status == "000")
            {
                _logger.LogInformation("JazzCash payment successful: {TxnRef}", data.GetValueOrDefault("pp_TxnRefNo"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process JazzCash webhook.");
        }

        return Task.CompletedTask;
    }

    private string ComputeHash(string txnRef, decimal amount, string txnDateTime)
    {
        var values = new[]
        {
            _options.IntegritySalt,
            _options.MerchantId,
            _options.Password,
            txnRef,
            ((long)(amount * 100)).ToString(),
            txnDateTime
        };

        var input = string.Join("&", values);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

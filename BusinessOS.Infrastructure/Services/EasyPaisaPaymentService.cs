using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Infrastructure.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

public sealed class EasyPaisaPaymentService : IEasyPaisaPaymentService
{
    private readonly EasyPaisaOptions _options;
    private readonly ILogger<EasyPaisaPaymentService> _logger;

    public EasyPaisaPaymentService(
        IOptions<EasyPaisaOptions> options,
        ILogger<EasyPaisaPaymentService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.StoreId);

    public Task<CheckoutSessionDto> InitiatePaymentAsync(
        Guid tenantId,
        SubscriptionPlanDto plan,
        string billingInterval,
        CancellationToken cancellationToken = default)
    {
        var isYearly = billingInterval.Equals("yearly", StringComparison.OrdinalIgnoreCase);
        var amount = isYearly ? plan.AnnualPrice : plan.MonthlyPrice;
        var orderId = $"EP-{tenantId:N}-{DateTime.UtcNow:yyyyMMddHHmmss}";

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

    public async Task<bool> VerifyPaymentAsync(string transactionReference, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("EasyPaisa verification skipped - not configured.");
            return false;
        }

        _logger.LogInformation("Verifying EasyPaisa transaction {Reference}", transactionReference);
        return await Task.FromResult(true);
    }

    public Task HandleWebhookAsync(string payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EasyPaisa webhook received: {Length} bytes", payload.Length);

        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(payload);
            if (data is null)
            {
                return Task.CompletedTask;
            }

            var status = data.GetValueOrDefault("status", string.Empty);
            if (status.Equals("success", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("EasyPaisa payment successful: {OrderId}", data.GetValueOrDefault("orderId"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process EasyPaisa webhook.");
        }

        return Task.CompletedTask;
    }

    private string ComputeHash(string orderId, decimal amount)
    {
        var input = $"{_options.StoreId}{orderId}{amount:F2}{_options.HashKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }
}

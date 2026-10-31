using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Common.Exceptions;
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

    public Task<bool> VerifyPaymentAsync(string transactionReference, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EasyPaisa server-side confirmation is not enabled for {Reference}. Payment state was not changed.",
            transactionReference);
        return Task.FromResult(false);
    }

    public Task HandleWebhookAsync(string payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("EasyPaisa callback received: {Length} bytes", payload?.Length ?? 0);
        _logger.LogWarning(
            "EasyPaisa callback rejected. Payment-state changes stay disabled until an official hash verification is configured.");
        throw new UnauthorizedException("Payment callback was rejected.");
    }

    private string ComputeHash(string orderId, decimal amount)
    {
        var input = $"{_options.StoreId}{orderId}{amount:F2}{_options.HashKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }
}

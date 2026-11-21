using BusinessOS.Infrastructure.Payments;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Diagnostics;

/// <summary>
/// Configuration probe for Stripe. Live network calls are skipped when credentials are absent
/// so local and CI environments stay green without payment secrets.
/// </summary>
public sealed class StripeHealthCheck : IHealthCheck
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripeHealthCheck> _logger;

    public StripeHealthCheck(IOptions<StripeOptions> options, ILogger<StripeHealthCheck> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var hasSecret = !string.IsNullOrWhiteSpace(_options.SecretKey);
        var hasWebhook = !string.IsNullOrWhiteSpace(_options.WebhookSecret);

        if (!hasSecret && !hasWebhook)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "Stripe credentials are not configured. Live checkout and webhooks are unavailable."));
        }

        if (!hasSecret || !hasWebhook)
        {
            _logger.LogWarning(
                "Stripe configuration is incomplete (secret={HasSecret}, webhook={HasWebhook}).",
                hasSecret,
                hasWebhook);
            return Task.FromResult(HealthCheckResult.Degraded(
                "Stripe configuration is incomplete. Live billing may fail."));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Stripe secret and webhook credentials are configured."));
    }
}

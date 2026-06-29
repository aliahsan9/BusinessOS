using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Application.Common.Interfaces;

namespace BusinessOS.Infrastructure.Services;

public sealed class BillingWebhookService : IBillingWebhookService
{
    private readonly IStripePaymentService _stripeService;
    private readonly IJazzCashPaymentService _jazzCashService;
    private readonly IEasyPaisaPaymentService _easyPaisaService;

    public BillingWebhookService(
        IStripePaymentService stripeService,
        IJazzCashPaymentService jazzCashService,
        IEasyPaisaPaymentService easyPaisaService)
    {
        _stripeService = stripeService;
        _jazzCashService = jazzCashService;
        _easyPaisaService = easyPaisaService;
    }

    public Task HandleStripeWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default) =>
        _stripeService.HandleWebhookAsync(payload, signature, cancellationToken);

    public Task HandleJazzCashWebhookAsync(string payload, CancellationToken cancellationToken = default) =>
        _jazzCashService.HandleWebhookAsync(payload, cancellationToken);

    public Task HandleEasyPaisaWebhookAsync(string payload, CancellationToken cancellationToken = default) =>
        _easyPaisaService.HandleWebhookAsync(payload, cancellationToken);
}

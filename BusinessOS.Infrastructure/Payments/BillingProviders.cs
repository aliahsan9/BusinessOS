using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Billing.DTOs;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Payments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Payments;

public sealed class StripeBillingProvider : IPaymentProvider
{
    private static readonly HashSet<string> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "AED", "GBP", "EUR", "CAD", "AUD", "SGD"
    };

    private readonly IStripePaymentService _stripe;

    public StripeBillingProvider(IStripePaymentService stripe) => _stripe = stripe;

    public PaymentProviderType ProviderType => PaymentProviderType.Stripe;
    public string Name => "stripe";
    public bool IsConfigured => _stripe.IsConfigured;
    public bool SupportsRecurring => true;
    public IReadOnlySet<string> ChargeableCurrencies => Currencies;
    public bool CanCharge(string currency) => Currencies.Contains(currency);

    public Task<CheckoutSessionDto> CreateSubscriptionCheckoutAsync(
        ProviderCheckoutCommand command,
        CancellationToken cancellationToken = default) =>
        _stripe.CreateCheckoutSessionAsync(
            command.TenantId,
            command.TenantName,
            command.Email,
            command.Plan,
            command.BillingInterval,
            command.SuccessUrl,
            command.CancelUrl,
            command.ExistingCustomerId,
            cancellationToken,
            command.OperationKey,
            command.Currency);
}

public sealed class TapPaymentsProvider : IPaymentProvider
{
    public const string ChargesPath = "charges";

    private static readonly HashSet<string> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "AED", "SAR", "QAR", "KWD", "BHD", "OMR"
    };

    private readonly HttpClient _http;
    private readonly TapOptions _options;
    private readonly IHostEnvironment _environment;

    public TapPaymentsProvider(HttpClient http, IOptions<TapOptions> options, IHostEnvironment environment)
    {
        _http = http;
        _options = options.Value;
        _environment = environment;
    }

    public PaymentProviderType ProviderType => PaymentProviderType.Tap;
    public string Name => "tap";
    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.SecretKey);
    public bool SupportsRecurring => _options.RecurringEnabled;
    public IReadOnlySet<string> ChargeableCurrencies => Currencies;
    public bool CanCharge(string currency) => IsConfigured && Currencies.Contains(currency);

    public async Task<CheckoutSessionDto> CreateSubscriptionCheckoutAsync(
        ProviderCheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        var amount = CurrencyMath.Round(command.Amount, command.Currency);
        var payload = new
        {
            amount,
            currency = command.Currency,
            customer_initiated = true,
            threeDSecure = true,
            save_card = false,
            description = $"BusinessOS {command.Plan.Name}",
            metadata = new Dictionary<string, string>
            {
                ["tenant_id"] = command.TenantId.ToString(),
                ["plan_id"] = command.Plan.Id.ToString(),
                ["operation_key"] = command.OperationKey ?? string.Empty
            },
            reference = new { transaction = command.OperationKey, order = command.Plan.Slug },
            customer = new { first_name = command.TenantName, email = command.Email },
            source = new { id = "src_all" },
            redirect = new { url = command.SuccessUrl }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ChargesPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new BadRequestException("Tap Payments did not start the checkout. No charge was recorded as paid.");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
        var url = root.TryGetProperty("transaction", out var transaction) &&
                  transaction.TryGetProperty("url", out var urlValue)
            ? urlValue.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url))
            throw new BadRequestException("Tap Payments did not return a hosted checkout URL.");

        return new CheckoutSessionDto
        {
            SessionId = id,
            CheckoutUrl = url,
            Provider = "tap",
            Amount = amount,
            Currency = command.Currency,
            PlanId = command.Plan.Id,
            BillingInterval = command.BillingInterval
        };
    }

    private void EnsureReady()
    {
        if (!IsConfigured)
            throw new BadRequestException("Tap Payments is not configured. Merchant credentials are required before a charge can be created.");
        if (_environment.IsDevelopment() && !_options.Sandbox)
            throw new BadRequestException("Development only allows Tap Payments sandbox mode.");
        if (_environment.IsDevelopment() && _options.SecretKey.StartsWith("sk_live", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Development cannot use a live Tap secret key.");
    }
}

public sealed class PayTabsPaymentProvider : IPaymentProvider
{
    private static readonly HashSet<string> Currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "AED", "SAR", "QAR", "KWD", "BHD", "OMR", "USD", "EUR", "GBP"
    };

    private readonly HttpClient _http;
    private readonly PayTabsOptions _options;
    private readonly BillingOptions _billing;
    private readonly IHostEnvironment _environment;

    public PayTabsPaymentProvider(
        HttpClient http,
        IOptions<PayTabsOptions> options,
        IOptions<BillingOptions> billing,
        IHostEnvironment environment)
    {
        _http = http;
        _options = options.Value;
        _billing = billing.Value;
        _environment = environment;
    }

    public PaymentProviderType ProviderType => PaymentProviderType.PayTabs;
    public string Name => "paytabs";
    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ServerKey) && !string.IsNullOrWhiteSpace(_options.ProfileId);
    public bool SupportsRecurring => _options.RecurringEnabled;
    public IReadOnlySet<string> ChargeableCurrencies => Currencies;
    public bool CanCharge(string currency) => IsConfigured && Currencies.Contains(currency);

    public async Task<CheckoutSessionDto> CreateSubscriptionCheckoutAsync(
        ProviderCheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new BadRequestException("PayTabs is not configured. An approved profile id and server key are required.");
        if (_environment.IsDevelopment() && !_options.Sandbox)
            throw new BadRequestException("Development only allows PayTabs sandbox mode.");

        if (!int.TryParse(_options.ProfileId, out var profileId))
            throw new BadRequestException("PayTabs profile id must be the numeric profile from the merchant dashboard.");

        var amount = CurrencyMath.Round(command.Amount, command.Currency);
        var payload = new
        {
            profile_id = profileId,
            tran_type = "sale",
            tran_class = "ecom",
            cart_id = command.OperationKey ?? Guid.NewGuid().ToString("N"),
            cart_currency = command.Currency,
            cart_amount = amount,
            cart_description = $"BusinessOS {command.Plan.Name}",
            hide_shipping = true,
            callback = _billing.WebhookBaseUrl.TrimEnd('/') + "/api/v1/billing/webhook/paytabs",
            @return = command.SuccessUrl,
            customer_details = new
            {
                name = command.TenantName,
                email = command.Email,
                country = string.IsNullOrWhiteSpace(_options.MerchantCountry) ? "SA" : _options.MerchantCountry
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "payment/request");
        request.Headers.TryAddWithoutValidation("Authorization", _options.ServerKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new BadRequestException("PayTabs did not start the hosted payment page. No charge was recorded as paid.");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var url = root.TryGetProperty("redirect_url", out var urlValue) ? urlValue.GetString() : null;
        var reference = root.TryGetProperty("tran_ref", out var referenceValue) ? referenceValue.GetString() : null;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(reference))
            throw new BadRequestException("PayTabs did not return a hosted payment URL.");

        return new CheckoutSessionDto
        {
            SessionId = reference,
            CheckoutUrl = url,
            Provider = "paytabs",
            Amount = amount,
            Currency = command.Currency,
            PlanId = command.Plan.Id,
            BillingInterval = command.BillingInterval,
            PriceNote = _options.RecurringEnabled
                ? null
                : "PayTabs checkout collects this period. Automatic renewal stays off unless the merchant profile supports agreements."
        };
    }

    public static string? RegionBaseUrl(string? region) => region?.Trim().ToLowerInvariant() switch
    {
        "ae" or "global" => "https://secure.paytabs.com/",
        "sa" => "https://secure.paytabs.sa/",
        "eg" => "https://secure-egypt.paytabs.com/",
        "om" => "https://secure-oman.paytabs.com/",
        "jo" => "https://secure-jordan.paytabs.com/",
        "iq" => "https://secure-iraq.paytabs.com/",
        _ => null
    };
}

public sealed class PaymentProviderSelector : IPaymentProviderSelector
{
    private readonly IReadOnlyList<IPaymentProvider> _providers;

    public PaymentProviderSelector(IEnumerable<IPaymentProvider> providers) =>
        _providers = providers.ToList();

    public IReadOnlyList<IPaymentProvider> Providers => _providers;

    public IPaymentProvider Require(string? requestedProvider, string currency)
    {
        var name = string.IsNullOrWhiteSpace(requestedProvider) ? "stripe" : requestedProvider.Trim();
        var provider = _providers.FirstOrDefault(x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            x.ProviderType.ToString().Equals(name, StringComparison.OrdinalIgnoreCase));

        if (provider is null || !provider.IsConfigured || !provider.CanCharge(currency))
        {
            throw new BadRequestException(
                "The selected payment provider cannot charge this currency with the current merchant configuration.");
        }

        return provider;
    }
}

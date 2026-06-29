namespace BusinessOS.Infrastructure.Payments;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "http://localhost:4200/billing?success=true";
    public string CancelUrl { get; set; } = "http://localhost:4200/pricing?cancelled=true";
    public string BillingPortalReturnUrl { get; set; } = "http://localhost:4200/billing";
}

public class JazzCashOptions
{
    public const string SectionName = "JazzCash";

    public bool Enabled { get; set; }
    public bool Sandbox { get; set; } = true;
    public string MerchantId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string IntegritySalt { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "http://localhost:4200/billing?provider=jazzcash";
    public string PostUrl { get; set; } = "https://sandbox.jazzcash.com.pk/CustomerPortal/transactionmanagement/merchantform";
}

public class EasyPaisaOptions
{
    public const string SectionName = "EasyPaisa";

    public bool Enabled { get; set; }
    public bool Sandbox { get; set; } = true;
    public string StoreId { get; set; } = string.Empty;
    public string HashKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "http://localhost:4200/billing?provider=easypaisa";
    public string PostUrl { get; set; } = "https://easypay.easypaisa.com.pk/easypay/Index.jsf";
}

public class BillingOptions
{
    public const string SectionName = "Billing";

    public decimal TaxRate { get; set; } = 0m;
    public int TrialDays { get; set; } = 14;
}

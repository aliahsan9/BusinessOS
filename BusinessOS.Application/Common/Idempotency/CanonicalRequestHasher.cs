using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BusinessOS.Application.Common.Idempotency;

public static class IdempotencyOperations
{
    public const string PaymentCreate = "payments.create";
    public const string PaymentRefund = "payments.refund";
    public const string BillingCheckout = "billing.checkout";
    public const string BillingRefund = "billing.refund";
}

public static class IdempotencyKeyRules
{
    public const int MaxLength = 128;

    public static bool IsValid(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxLength)
            return false;

        foreach (var character in key)
        {
            if (character is < (char)33 or > (char)126)
                return false;
        }

        return true;
    }
}

public static class ProviderOperationKey
{
    public static string Create(Guid operationId, string provider)
    {
        var hex = operationId.ToString("N");
        return provider.Trim().ToLowerInvariant() switch
        {
            "jazzcash" => hex[..20],
            "easypaisa" => ("EP" + hex)[..20],
            _ => "bos-" + hex
        };
    }
}

public static class CanonicalRequestHasher
{
    public static string PaymentCreate(
        Guid tenantId,
        Guid orderId,
        Guid customerId,
        decimal amount,
        string currency,
        string paymentMethod,
        DateTime paymentDate,
        string? referenceNo) =>
        Hash(
            "payments.create",
            tenantId.ToString("D"),
            orderId.ToString("D"),
            customerId.ToString("D"),
            Money(amount),
            NormalizeToken(currency),
            paymentMethod.Trim(),
            paymentDate.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            (referenceNo ?? string.Empty).Trim());

    public static string PaymentRefund(Guid tenantId, Guid paymentId, decimal amount, string currency) =>
        Hash(
            "payments.refund",
            tenantId.ToString("D"),
            paymentId.ToString("D"),
            Money(amount),
            NormalizeToken(currency));

    public static string BillingCheckout(
        Guid tenantId,
        Guid planId,
        string billingInterval,
        string provider,
        decimal amount,
        string currency) =>
        Hash(
            "billing.checkout",
            tenantId.ToString("D"),
            planId.ToString("D"),
            billingInterval.Trim().ToLowerInvariant(),
            provider.Trim().ToLowerInvariant(),
            Money(amount),
            NormalizeToken(currency));

    public static string BillingRefund(Guid tenantId, Guid transactionId, decimal amount, string currency) =>
        Hash(
            "billing.refund",
            tenantId.ToString("D"),
            transactionId.ToString("D"),
            Money(amount),
            NormalizeToken(currency));

    public static string Hash(params string[] fields)
    {
        var canonical = string.Join('\n', fields);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }

    private static string Money(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string NormalizeToken(string value) =>
        value.Trim().ToUpperInvariant();
}

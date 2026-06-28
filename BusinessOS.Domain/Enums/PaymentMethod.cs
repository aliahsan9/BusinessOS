namespace BusinessOS.Domain.Enums;

public enum PaymentMethod
{
    Cash,
    BankTransfer,
    CreditCard,
    DebitCard,
    Cheque,
    OnlinePayment
}

public static class PaymentMethodNames
{
    public const string Cash = nameof(PaymentMethod.Cash);
    public const string BankTransfer = nameof(PaymentMethod.BankTransfer);
    public const string CreditCard = nameof(PaymentMethod.CreditCard);
    public const string DebitCard = nameof(PaymentMethod.DebitCard);
    public const string Cheque = nameof(PaymentMethod.Cheque);
    public const string OnlinePayment = nameof(PaymentMethod.OnlinePayment);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Cash,
        BankTransfer,
        CreditCard,
        DebitCard,
        Cheque,
        OnlinePayment
    };

    public static bool IsValid(string? paymentMethod) =>
        paymentMethod is not null && All.Contains(paymentMethod);
}

using BusinessOS.Domain.Enums;

namespace BusinessOS.Domain.Billing;

public static class SubscriptionStatusNames
{
    public const string Free = "Free";
    public const string Trialing = "Trialing";
    public const string Active = "Active";
    public const string PastDue = "PastDue";
    public const string Canceled = "Canceled";
    public const string Expired = "Expired";
    public const string PendingPayment = "PendingPayment";
    public const string Incomplete = "Incomplete";

    public static string ToApi(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Trial => Trialing,
        SubscriptionStatus.Cancelled => Canceled,
        SubscriptionStatus.Free => Free,
        SubscriptionStatus.Active => Active,
        SubscriptionStatus.PastDue => PastDue,
        SubscriptionStatus.Expired => Expired,
        SubscriptionStatus.PendingPayment => PendingPayment,
        SubscriptionStatus.Incomplete => Incomplete,
        _ => status.ToString()
    };

    public static bool TryParse(string? value, out SubscriptionStatus status)
    {
        status = SubscriptionStatus.Free;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "free":
                status = SubscriptionStatus.Free;
                return true;
            case "trial":
            case "trialing":
                status = SubscriptionStatus.Trial;
                return true;
            case "active":
                status = SubscriptionStatus.Active;
                return true;
            case "pastdue":
            case "past_due":
                status = SubscriptionStatus.PastDue;
                return true;
            case "canceled":
            case "cancelled":
                status = SubscriptionStatus.Cancelled;
                return true;
            case "expired":
                status = SubscriptionStatus.Expired;
                return true;
            case "pendingpayment":
            case "pending_payment":
                status = SubscriptionStatus.PendingPayment;
                return true;
            case "incomplete":
                status = SubscriptionStatus.Incomplete;
                return true;
            default:
                return false;
        }
    }
}

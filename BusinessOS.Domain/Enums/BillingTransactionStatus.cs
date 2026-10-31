namespace BusinessOS.Domain.Enums;

public enum BillingTransactionStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3,
    Cancelled = 4,
    RequiresReconciliation = 5
}

public static class BillingTransactionTransitions
{
    public static bool CanApply(BillingTransactionStatus current, BillingTransactionStatus next)
    {
        if (current == next)
            return false;

        return (current, next) switch
        {
            (BillingTransactionStatus.Pending, BillingTransactionStatus.Completed) => true,
            (BillingTransactionStatus.Pending, BillingTransactionStatus.Failed) => true,
            (BillingTransactionStatus.Pending, BillingTransactionStatus.RequiresReconciliation) => true,
            (BillingTransactionStatus.Pending, BillingTransactionStatus.Cancelled) => true,
            (BillingTransactionStatus.RequiresReconciliation, BillingTransactionStatus.Completed) => true,
            (BillingTransactionStatus.RequiresReconciliation, BillingTransactionStatus.Failed) => true,
            (BillingTransactionStatus.Failed, BillingTransactionStatus.RequiresReconciliation) => true,
            (BillingTransactionStatus.Completed, BillingTransactionStatus.Refunded) => true,
            _ => false
        };
    }

    /// <summary>
    /// Verified provider events may confirm a payment that an earlier timeout marked as failed.
    /// They must not move a completed or refunded payment backwards.
    /// </summary>
    public static bool CanApplyVerifiedProviderResult(BillingTransactionStatus current, BillingTransactionStatus next)
    {
        if (current == next)
            return false;

        if (current is BillingTransactionStatus.Completed or BillingTransactionStatus.Refunded &&
            next is not BillingTransactionStatus.Refunded)
        {
            return current == BillingTransactionStatus.Completed && next == BillingTransactionStatus.Refunded;
        }

        return next is BillingTransactionStatus.Completed
            or BillingTransactionStatus.Failed
            or BillingTransactionStatus.RequiresReconciliation
            or BillingTransactionStatus.Refunded;
    }
}

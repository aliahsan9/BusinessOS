namespace BusinessOS.Application.Common.Exceptions;

public sealed class PlanLimitExceededException : AppException
{
    public string ErrorCode { get; } = ErrorCodes.PlanLimitExceeded;
    public string Resource { get; }
    public long CurrentUsage { get; }
    public long Maximum { get; }
    public string UpgradeSuggestion { get; }

    public PlanLimitExceededException(string resource, long currentUsage, long maximum, string? suggestion = null)
        : base($"{resource} limit reached ({currentUsage} of {maximum}). Upgrade your plan to continue.")
    {
        Resource = resource;
        CurrentUsage = currentUsage;
        Maximum = maximum;
        UpgradeSuggestion = suggestion ?? "Upgrade your subscription to increase this allowance.";
    }
}

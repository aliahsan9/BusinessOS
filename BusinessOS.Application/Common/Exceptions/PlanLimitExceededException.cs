namespace BusinessOS.Application.Common.Exceptions;

public sealed class PlanLimitExceededException : AppException
{
    public string ErrorCode { get; } = ErrorCodes.PlanLimitExceeded;
    public string Resource { get; }
    public long CurrentUsage { get; }
    public long Maximum { get; }
    public long Remaining { get; }
    public string UpgradeSuggestion { get; }

    public PlanLimitExceededException(string resource, long currentUsage, long maximum, string? suggestion = null)
        : base(BuildMessage(resource, maximum))
    {
        Resource = Normalize(resource);
        CurrentUsage = currentUsage;
        Maximum = maximum;
        Remaining = maximum < 0 ? 0 : Math.Max(0, maximum - currentUsage);
        UpgradeSuggestion = suggestion ?? "Upgrade your subscription to increase this allowance.";
    }

    public static string Normalize(string resource) => resource.Trim().ToLowerInvariant() switch
    {
        "user" or "users" => "Users",
        "customer" or "customers" => "Customers",
        "product" or "products" => "Products",
        "project" or "projects" => "Projects",
        "task" or "tasks" => "Tasks",
        "ai" or "airequests" or "ai requests" => "AI requests",
        "storage" => "Storage",
        _ => resource.Trim()
    };

    public static string BuildMessage(string resource, long maximum)
    {
        var name = Normalize(resource);
        var allowance = maximum < 0 ? "unlimited" : maximum.ToString("N0");
        return name switch
        {
            "Users" => $"Your plan allows a maximum of {allowance} users. Upgrade your plan to add more users.",
            "Customers" => $"Your plan allows a maximum of {allowance} customers. Upgrade your plan to add more customers.",
            "Products" => $"Your plan allows a maximum of {allowance} products. Upgrade your plan to add more products.",
            "Projects" => $"Your plan allows a maximum of {allowance} projects. Upgrade your plan to add more projects.",
            "Tasks" => $"Your plan allows a maximum of {allowance} tasks. Upgrade your plan to add more tasks.",
            "Storage" => $"Your plan allows a maximum of {allowance} MB of storage. Upgrade your plan to upload more files.",
            "AI requests" => $"Your plan allows a maximum of {allowance} AI requests this period. Upgrade your plan to continue.",
            _ => $"Your plan allows a maximum of {allowance} {name}. Upgrade your plan to continue."
        };
    }
}

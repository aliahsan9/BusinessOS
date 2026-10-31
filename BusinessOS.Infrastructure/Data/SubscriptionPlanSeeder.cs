using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Data;

public static class SubscriptionPlanSeeder
{
    public static readonly Guid FreePlanId = Domain.Billing.SubscriptionCatalog.FreePlanId;
    public static readonly Guid StarterPlanId = Domain.Billing.SubscriptionCatalog.StarterPlanId;
    public static readonly Guid ProfessionalPlanId = Domain.Billing.SubscriptionCatalog.ProfessionalPlanId;
    public static readonly Guid EnterprisePlanId = Domain.Billing.SubscriptionCatalog.EnterprisePlanId;

    public static Task SeedAsync(
        BusinessOSDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default) =>
        SubscriptionCatalogMigrator.ApplyAsync(context, logger, cancellationToken);
}

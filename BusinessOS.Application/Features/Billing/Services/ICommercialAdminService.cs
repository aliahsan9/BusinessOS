using BusinessOS.Application.Features.Billing.DTOs;

namespace BusinessOS.Application.Features.Billing.Services;

public interface ICommercialAdminService
{
    Task<SubscriptionPlanDto> UpdatePlanAsync(Guid planId, CommercialPlanUpdateRequest request, CancellationToken cancellationToken = default);
    Task UpsertPlanFeatureAsync(Guid planId, string featureKey, FeatureGrantRequest request, CancellationToken cancellationToken = default);
    Task UpsertCompanyFeatureAsync(Guid companyId, string featureKey, FeatureGrantRequest request, CancellationToken cancellationToken = default);
    Task<CurrentPlanDto> GrantTrialAsync(Guid companyId, GrantTrialRequest request, CancellationToken cancellationToken = default);
    Task<CurrentPlanDto> SuspendSubscriptionAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CurrentPlanDto> ReactivateSubscriptionAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FailedPaymentDto>> ListFailedPaymentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelPriceDto>> ListModelPricesAsync(CancellationToken cancellationToken = default);
    Task<ModelPriceDto> UpsertModelPriceAsync(ModelPriceRequest request, CancellationToken cancellationToken = default);
}

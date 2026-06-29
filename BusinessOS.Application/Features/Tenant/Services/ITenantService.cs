using BusinessOS.Application.Features.Tenant.DTOs;

namespace BusinessOS.Application.Features.Tenant.Services;

public interface ITenantService
{
    Task<TenantDto> GetTenantAsync(CancellationToken cancellationToken = default);
    Task<TenantDto> UpdateTenantAsync(UpdateTenantRequest request, CancellationToken cancellationToken = default);
    Task<TenantSettingsDto> GetTenantSettingsAsync(CancellationToken cancellationToken = default);
    Task<TenantSettingsDto> UpdateTenantSettingsAsync(UpdateTenantSettingsRequest request, CancellationToken cancellationToken = default);
    Task<TenantUsageDto> GetTenantUsageAsync(CancellationToken cancellationToken = default);
    Task<TenantDashboardDto> GetTenantDashboardAsync(CancellationToken cancellationToken = default);
}

using BusinessOS.Application.Features.Settings.DTOs;

namespace BusinessOS.Application.Features.Settings.Services;

public interface ISettingsService
{
    Task<TenantSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<TenantSettingsDto> UpdateSettingsAsync(
        UpdateTenantSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<BusinessProfileDto> GetBusinessProfileAsync(CancellationToken cancellationToken = default);

    Task<BusinessProfileDto> UpdateBusinessProfileAsync(
        UpdateBusinessProfileRequest request,
        CancellationToken cancellationToken = default);
}

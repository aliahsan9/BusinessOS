using BusinessOS.Application.Features.SystemAdmin.DTOs;

namespace BusinessOS.Application.Features.SystemAdmin.Services;

public interface ISystemAdminService
{
    Task<SystemHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<SystemStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<EnvironmentInfoResponse> GetEnvironmentInfoAsync(CancellationToken cancellationToken = default);
}

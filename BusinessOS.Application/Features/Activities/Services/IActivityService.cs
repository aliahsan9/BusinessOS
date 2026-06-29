using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Activities.DTOs;

namespace BusinessOS.Application.Features.Activities.Services;

public interface IActivityService
{
    Task<PagedResult<ActivityResponse>> GetActivitiesAsync(
        ActivityQueryParams query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityResponse>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<ActivityResponse> LogAsync(
        LogActivityRequest request,
        CancellationToken cancellationToken = default);
}

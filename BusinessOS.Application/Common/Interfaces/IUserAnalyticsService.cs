namespace BusinessOS.Application.Common.Interfaces;

public interface IUserAnalyticsService
{
    Task<int> GetActiveUserCountAsync(CancellationToken cancellationToken = default);
}

namespace BusinessOS.Application.Common.Interfaces;

public interface IAuthorizationVersionService
{
    Task BumpUserAsync(string userId, CancellationToken cancellationToken = default);

    Task BumpUsersWithRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}

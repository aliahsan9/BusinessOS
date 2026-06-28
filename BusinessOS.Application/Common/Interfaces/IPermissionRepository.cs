using BusinessOS.Domain.Entities;

namespace BusinessOS.Application.Common.Interfaces;

public interface IPermissionRepository
{
    Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    Task<Permission?> GetPermissionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Permission?> GetPermissionByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Permission>> GetPermissionsByCodesAsync(
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default);
}

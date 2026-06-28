using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Repositories;

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly BusinessOSDbContext _context;

    public PermissionRepository(BusinessOSDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
        await _context.Permissions
            .AsNoTracking()
            .OrderBy(x => x.Category)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

    public async Task<Permission?> GetPermissionByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Permission?> GetPermissionByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetPermissionsByCodesAsync(
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default)
    {
        var codeList = codes.ToList();
        return await _context.Permissions
            .AsNoTracking()
            .Where(x => codeList.Contains(x.Code))
            .ToListAsync(cancellationToken);
    }
}
